using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

public sealed partial class UncropFillService
{
    public Mat FillEdgeGradient(
        Mat sourceBgr,
        CanvasPadding padding,
        UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay,
        Scalar? customEndColor = null,
        double noiseAmount = 0.0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var canvasSize = padding.ExpandedSize(sourceBgr.Size());
        int totalW = canvasSize.Width;
        int totalH = canvasSize.Height;

        var result = new Mat(totalH, totalW, MatType.CV_8UC3, Scalar.All(0));

        // Sample border colors
        var topColor = SampleLineAverageColor(sourceBgr, new Rect(0, 0, sourceBgr.Width, Math.Min(3, sourceBgr.Height)));
        var bottomColor = SampleLineAverageColor(sourceBgr, new Rect(0, Math.Max(0, sourceBgr.Height - 3), sourceBgr.Width, Math.Min(3, sourceBgr.Height)));
        var leftColor = SampleLineAverageColor(sourceBgr, new Rect(0, 0, Math.Min(3, sourceBgr.Width), sourceBgr.Height));
        var rightColor = SampleLineAverageColor(sourceBgr, new Rect(Math.Max(0, sourceBgr.Width - 3), 0, Math.Min(3, sourceBgr.Width), sourceBgr.Height));

        Scalar endColor = customEndColor ?? SampleEdgeAverageColor(sourceBgr);

        // Rows are independent per-pixel math over the padded canvas, so they run in parallel;
        // the gradient interpolation, distance falloff and noise are identical to the sequential
        // pass (Random.Shared is thread-safe). Cancellation flows through the parallel options so
        // the callers' OperationCanceledException handling is preserved.
        unsafe
        {
            PixelLoop.ForEachRowParallel(result, (rowPtr, y) =>
            {
                byte* rowBase = (byte*)rowPtr;
                for (int x = 0; x < totalW; x++)
                {
                    // Check if inside interior
                    if (x >= padding.Left && x < padding.Left + sourceBgr.Width &&
                        y >= padding.Top && y < padding.Top + sourceBgr.Height)
                    {
                        continue;
                    }

                    Scalar c;
                    if (gradientMode == UncropGradientMode.FourCorners)
                    {
                        // Bilinear interpolation across whole canvas using 4 edge/corner colors
                        float tx = totalW > 1 ? (float)x / (totalW - 1) : 0f;
                        float ty = totalH > 1 ? (float)y / (totalH - 1) : 0f;

                        // Top blend, bottom blend, then vertical
                        var cTop = Interpolate(leftColor, rightColor, tx);
                        var cBottom = Interpolate(leftColor, rightColor, tx);
                        var cVert = Interpolate(topColor, bottomColor, ty);
                        c = Interpolate(Interpolate(cTop, cBottom, ty), cVert, 0.5f);
                    }
                    else if (gradientMode == UncropGradientMode.FadeToColor)
                    {
                        // Distance to nearest image border
                        float dist = GetDistanceToInterior(x, y, padding, sourceBgr.Width, sourceBgr.Height, out Scalar nearestEdgeColor, topColor, bottomColor, leftColor, rightColor);
                        float maxDist = Math.Max(1f, Math.Max(Math.Max(padding.Left, padding.Right), Math.Max(padding.Top, padding.Bottom)));
                        float t = Math.Clamp(dist / maxDist, 0f, 1f);
                        c = Interpolate(nearestEdgeColor, endColor, t);
                    }
                    else // PerEdgeSplay
                    {
                        GetDistanceToInterior(x, y, padding, sourceBgr.Width, sourceBgr.Height, out Scalar nearestEdgeColor, topColor, bottomColor, leftColor, rightColor);
                        c = nearestEdgeColor;
                    }

                    byte b = (byte)Math.Clamp(Math.Round(c.Val0), 0, 255);
                    byte g = (byte)Math.Clamp(Math.Round(c.Val1), 0, 255);
                    byte r = (byte)Math.Clamp(Math.Round(c.Val2), 0, 255);

                    if (noiseAmount > 0.001)
                    {
                        double n = (Random.Shared.NextDouble() - 0.5) * noiseAmount * 255.0;
                        b = (byte)Math.Clamp(b + n, 0, 255);
                        g = (byte)Math.Clamp(g + n, 0, 255);
                        r = (byte)Math.Clamp(r + n, 0, 255);
                    }

                    byte* pixel = rowBase + x * 3;
                    pixel[0] = b;
                    pixel[1] = g;
                    pixel[2] = r;
                }
            }, ct);
        }

        ct.ThrowIfCancellationRequested();
        ImageProcessingUtility.RestoreInterior(result, sourceBgr, padding);

        return result;
    }

    private static Scalar SampleEdgeAverageColor(Mat sourceBgr)
    {
        int band = Math.Max(1, Math.Min(5, Math.Min(sourceBgr.Width, sourceBgr.Height) / 4));
        using var mask = new Mat(sourceBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Rect(0, 0, sourceBgr.Width, sourceBgr.Height), Scalar.All(255), thickness: band);
        return Cv2.Mean(sourceBgr, mask);
    }

    private static Scalar SampleLineAverageColor(Mat sourceBgr, Rect roi)
    {
        roi = new Rect(
            Math.Max(0, roi.X),
            Math.Max(0, roi.Y),
            Math.Min(roi.Width, sourceBgr.Width - roi.X),
            Math.Min(roi.Height, sourceBgr.Height - roi.Y));

        if (roi.Width <= 0 || roi.Height <= 0) return new Scalar(128, 128, 128);
        using var sub = new Mat(sourceBgr, roi);
        return Cv2.Mean(sub);
    }

    private static Scalar Interpolate(Scalar a, Scalar b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Scalar(
            a.Val0 + (b.Val0 - a.Val0) * t,
            a.Val1 + (b.Val1 - a.Val1) * t,
            a.Val2 + (b.Val2 - a.Val2) * t);
    }

    private static float GetDistanceToInterior(
        int x, int y,
        CanvasPadding padding,
        int innerW, int innerH,
        out Scalar edgeColor,
        Scalar topColor, Scalar bottomColor, Scalar leftColor, Scalar rightColor)
    {
        int intLeft = padding.Left;
        int intRight = padding.Left + innerW - 1;
        int intTop = padding.Top;
        int intBottom = padding.Top + innerH - 1;

        GeometryHelper.DistanceToRect(x, y, intLeft, intTop, intRight, intBottom, out int dx, out int dy);

        if (dy >= dx)
        {
            edgeColor = y < intTop ? topColor : bottomColor;
        }
        else
        {
            edgeColor = x < intLeft ? leftColor : rightColor;
        }

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static void FillBorderRegions(Mat expanded, CanvasPadding padding, Size sourceSize, Scalar color)
    {
        if (padding.Top > 0)
        {
            using var roi = new Mat(expanded, new Rect(0, 0, expanded.Width, padding.Top));
            roi.SetTo(color);
        }
        if (padding.Bottom > 0)
        {
            using var roi = new Mat(expanded, new Rect(0, padding.Top + sourceSize.Height, expanded.Width, padding.Bottom));
            roi.SetTo(color);
        }
        if (padding.Left > 0)
        {
            using var roi = new Mat(expanded, new Rect(0, padding.Top, padding.Left, sourceSize.Height));
            roi.SetTo(color);
        }
        if (padding.Right > 0)
        {
            using var roi = new Mat(expanded, new Rect(padding.Left + sourceSize.Width, padding.Top, padding.Right, sourceSize.Height));
            roi.SetTo(color);
        }
    }

    private static void ApplyFadeToEdge(Mat canvas, Mat sourceBgr, CanvasPadding padding, float fadeFactor, CancellationToken ct)
    {
        int w = canvas.Width;
        int h = canvas.Height;
        int intLeft = padding.Left;
        int intRight = padding.Left + sourceBgr.Width;
        int intTop = padding.Top;
        int intBottom = padding.Top + sourceBgr.Height;

        float maxDist = Math.Max(1f, Math.Max(Math.Max(padding.Left, padding.Right), Math.Max(padding.Top, padding.Bottom)));

        // Rows are independent per-pixel math, so they run in parallel (identical results to
        // the sequential pass); cancellation flows through the parallel options.
        unsafe
        {
            PixelLoop.ForEachRowParallel(canvas, (rowPtr, y) =>
            {
                byte* rowBase = (byte*)rowPtr;
                for (int x = 0; x < w; x++)
                {
                    if (x >= intLeft && x < intRight && y >= intTop && y < intBottom)
                    {
                        continue;
                    }

                    GeometryHelper.DistanceToRect(x, y, intLeft, intTop, intRight - 1, intBottom - 1, out int dx, out int dy);

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float t = Math.Clamp(dist / maxDist, 0f, 1f);
                    float multiplier = 1.0f - (1.0f - fadeFactor) * t;

                    byte* pixel = rowBase + x * 3;
                    pixel[0] = (byte)Math.Clamp(pixel[0] * multiplier, 0, 255);
                    pixel[1] = (byte)Math.Clamp(pixel[1] * multiplier, 0, 255);
                    pixel[2] = (byte)Math.Clamp(pixel[2] * multiplier, 0, 255);
                }
            }, ct);
        }
    }

    private static void BlendInteriorWithFeather(Mat resultCanvas, Mat sourceBgr, CanvasPadding padding, int featherPx, CancellationToken ct)
    {
        int w = sourceBgr.Width;
        int h = sourceBgr.Height;
        featherPx = Math.Max(1, Math.Min(featherPx, Math.Min(w, h) / 4));

        // Single-pass blend over the native buffers: the previous version built a float alpha
        // mask and ~7 intermediate CV_32FC3 Mats (a full-image float pass each). Blend math is
        // identical: result = source*factor + existing*(1-factor) near the edges, existing
        // (already-filled) content elsewhere. Rows are independent, so they run in parallel
        // (cancellation flows through ParallelOptions).
        unsafe
        {
            byte* srcBase = (byte*)sourceBgr.DataPointer;
            long srcStep = sourceBgr.Step();
            byte* dstBase = (byte*)resultCanvas.DataPointer;
            long dstStep = resultCanvas.Step();
            int ox = padding.Left;
            int oy = padding.Top;
            bool padL = padding.Left > 0, padR = padding.Right > 0, padT = padding.Top > 0, padB = padding.Bottom > 0;

            Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
            {
                var srcRow = new Span<Vec3b>((Vec3b*)(srcBase + y * srcStep), w);
                var dstRow = new Span<Vec3b>((Vec3b*)(dstBase + (oy + y) * dstStep + ox * 3), w);
                int distTop = y;
                int distBottom = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    int distLeft = x;
                    int distRight = w - 1 - x;

                    int minDist = int.MaxValue;
                    if (padL) minDist = Math.Min(minDist, distLeft);
                    if (padR) minDist = Math.Min(minDist, distRight);
                    if (padT) minDist = Math.Min(minDist, distTop);
                    if (padB) minDist = Math.Min(minDist, distBottom);

                    // The alpha mask is 1.0 everywhere except the feather band, so the whole
                    // interior is overwritten with the source; only the band blends with the
                    // pre-existing (background) content.
                    var src = srcRow[x];
                    Vec3b val;
                    if (minDist < featherPx)
                    {
                        float factor = (float)minDist / featherPx;
                        float inv = 1f - factor;
                        var cur = dstRow[x];
                        val = new Vec3b(
                            PixelColor.BlendWeighted(src.Item0, factor, cur.Item0, inv),
                            PixelColor.BlendWeighted(src.Item1, factor, cur.Item1, inv),
                            PixelColor.BlendWeighted(src.Item2, factor, cur.Item2, inv));
                    }
                    else
                    {
                        val = src;
                    }
                    dstRow[x] = val;
                }
            });
        }
    }

}
