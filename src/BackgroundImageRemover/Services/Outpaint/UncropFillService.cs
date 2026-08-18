using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

public sealed class UncropFillService : IUncropFillService
{
    public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
    {
        var expanded = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Constant, Scalar.All(0));

        var mask = new Mat(expanded.Size(), MatType.CV_8UC1, Scalar.All(255));
        using (var innerRoi = new Mat(mask, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height)))
        {
            innerRoi.SetTo(Scalar.All(0));
        }

        newAreaMask = mask;
        return expanded;
    }

    public Mat FillMirror(
        Mat sourceBgr,
        CanvasPadding padding,
        UncropMirrorType mirrorType = UncropMirrorType.Reflect101,
        int blurRadius = 0,
        double fadeOpacity = 1.0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        var borderType = mirrorType == UncropMirrorType.Reflect ? BorderTypes.Reflect : BorderTypes.Reflect101;
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right, borderType);
        ct.ThrowIfCancellationRequested();

        if (blurRadius > 0)
        {
            int kernel = blurRadius % 2 == 0 ? blurRadius + 1 : blurRadius;
            kernel = Math.Max(3, kernel);
            using var blurred = new Mat();
            Cv2.GaussianBlur(result, blurred, new Size(kernel, kernel), 0);
            ct.ThrowIfCancellationRequested();

            using var interiorRoi = new Mat(blurred, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
            result.Dispose();
            result = blurred.Clone();
        }

        if (fadeOpacity < 0.999)
        {
            ct.ThrowIfCancellationRequested();
            ApplyFadeToEdge(result, sourceBgr, padding, (float)Math.Clamp(fadeOpacity, 0.0, 1.0), ct);
        }

        ct.ThrowIfCancellationRequested();
        return result;
    }

    public Mat FillInpaint(
        Mat sourceBgr,
        CanvasPadding padding,
        UncropInpaintMethod method,
        double inpaintRadius = 5,
        int blendMargin = 0,
        bool preFillEdgeAverage = false,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var expanded = ExpandCanvas(sourceBgr, padding, out var mask);
        using (mask)
        {
            ct.ThrowIfCancellationRequested();
            if (preFillEdgeAverage)
            {
                var edgeColor = SampleEdgeAverageColor(sourceBgr);
                FillBorderRegions(expanded, padding, sourceBgr.Size(), edgeColor);
            }

            var result = new Mat();
            var cvMethod = method == UncropInpaintMethod.Telea ? InpaintMethod.Telea : InpaintMethod.NS;
            double radius = Math.Max(1.0, Math.Min(100.0, inpaintRadius));
            Cv2.Inpaint(expanded, mask, result, inpaintRadius: radius, cvMethod);

            ct.ThrowIfCancellationRequested();

            if (blendMargin <= 0)
            {
                using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
                sourceBgr.CopyTo(interiorRoi);
            }
            else
            {
                BlendInteriorWithFeather(result, sourceBgr, padding, blendMargin, ct);
            }

            ct.ThrowIfCancellationRequested();
            return result;
        }
    }

    public Mat FillSolidColor(
        Mat sourceBgr,
        CanvasPadding padding,
        bool blurred,
        Scalar? customColor = null,
        int blurRadius = 0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (padding.IsZero)
        {
            return sourceBgr.Clone();
        }

        if (blurred)
        {
            return FillSolidColorBlurred(sourceBgr, padding, blurRadius, ct);
        }

        var expanded = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Constant, Scalar.All(0));
        var color = customColor ?? SampleEdgeAverageColor(sourceBgr);
        ct.ThrowIfCancellationRequested();
        FillBorderRegions(expanded, padding, sourceBgr.Size(), color);
        ct.ThrowIfCancellationRequested();
        return expanded;
    }

    public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Replicate);
        ct.ThrowIfCancellationRequested();

        if (smoothRadius > 0)
        {
            int kernel = smoothRadius % 2 == 0 ? smoothRadius + 1 : smoothRadius;
            kernel = Math.Max(3, kernel);
            using var smoothed = new Mat();
            Cv2.GaussianBlur(result, smoothed, new Size(kernel, kernel), 0);
            ct.ThrowIfCancellationRequested();

            using var interiorRoi = new Mat(smoothed, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
            result.Dispose();
            result = smoothed.Clone();
        }

        return result;
    }

    public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Wrap);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    public Mat FillZoomBlur(
        Mat sourceBgr,
        CanvasPadding padding,
        int blurRadius = 25,
        double zoomScale = 1.25,
        int blendMargin = 0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        int totalW = sourceBgr.Width + padding.Left + padding.Right;
        int totalH = sourceBgr.Height + padding.Top + padding.Bottom;

        // Scale image to at least cover the total canvas size (cover mode) with zoom factor
        double scaleX = (double)totalW / sourceBgr.Width;
        double scaleY = (double)totalH / sourceBgr.Height;
        double baseScale = Math.Max(scaleX, scaleY) * Math.Max(1.0, Math.Min(3.0, zoomScale));

        int scaledW = (int)Math.Ceiling(sourceBgr.Width * baseScale);
        int scaledH = (int)Math.Ceiling(sourceBgr.Height * baseScale);

        using var zoomed = new Mat();
        Cv2.Resize(sourceBgr, zoomed, new Size(scaledW, scaledH), interpolation: InterpolationFlags.Linear);
        ct.ThrowIfCancellationRequested();

        // Crop centered ROI of size (totalW, totalH)
        int cropX = Math.Max(0, (scaledW - totalW) / 2);
        int cropY = Math.Max(0, (scaledH - totalH) / 2);
        cropX = Math.Min(cropX, scaledW - totalW);
        cropY = Math.Min(cropY, scaledH - totalH);

        using var bgRoi = new Mat(zoomed, new Rect(cropX, cropY, totalW, totalH));
        var result = bgRoi.Clone();

        // Apply blur to the background
        if (blurRadius > 0)
        {
            int kernel = blurRadius % 2 == 0 ? blurRadius + 1 : blurRadius;
            kernel = Math.Max(3, kernel);
            using var blurred = new Mat();
            Cv2.GaussianBlur(result, blurred, new Size(kernel, kernel), 0);
            result.Dispose();
            result = blurred.Clone();
        }

        ct.ThrowIfCancellationRequested();

        // Overlay original image in interior with optional feather
        if (blendMargin <= 0)
        {
            using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
        }
        else
        {
            BlendInteriorWithFeather(result, sourceBgr, padding, blendMargin, ct);
        }

        ct.ThrowIfCancellationRequested();
        return result;
    }

    public Mat FillEdgeGradient(
        Mat sourceBgr,
        CanvasPadding padding,
        UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay,
        Scalar? customEndColor = null,
        double noiseAmount = 0.0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        int totalW = sourceBgr.Width + padding.Left + padding.Right;
        int totalH = sourceBgr.Height + padding.Top + padding.Bottom;

        var result = new Mat(totalH, totalW, MatType.CV_8UC3, Scalar.All(0));

        // Sample border colors
        var topColor = SampleLineAverageColor(sourceBgr, new Rect(0, 0, sourceBgr.Width, Math.Min(3, sourceBgr.Height)));
        var bottomColor = SampleLineAverageColor(sourceBgr, new Rect(0, Math.Max(0, sourceBgr.Height - 3), sourceBgr.Width, Math.Min(3, sourceBgr.Height)));
        var leftColor = SampleLineAverageColor(sourceBgr, new Rect(0, 0, Math.Min(3, sourceBgr.Width), sourceBgr.Height));
        var rightColor = SampleLineAverageColor(sourceBgr, new Rect(Math.Max(0, sourceBgr.Width - 3), 0, Math.Min(3, sourceBgr.Width), sourceBgr.Height));

        Scalar endColor = customEndColor ?? SampleEdgeAverageColor(sourceBgr);

        unsafe
        {
            byte* ptr = (byte*)result.DataPointer;
            long step = result.Step();

            for (int y = 0; y < totalH; y++)
            {
                if (y % 32 == 0) ct.ThrowIfCancellationRequested();

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

                    byte* pixel = ptr + y * step + x * 3;
                    pixel[0] = b;
                    pixel[1] = g;
                    pixel[2] = r;
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
        sourceBgr.CopyTo(interiorRoi);

        return result;
    }

    public Mat FillPatchSynthesis(
        Mat sourceBgr,
        CanvasPadding padding,
        int patchSize = 32,
        int blendOverlap = 8,
        int blendMargin = 0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        patchSize = Math.Max(8, Math.Min(patchSize, Math.Min(sourceBgr.Width, sourceBgr.Height)));
        blendOverlap = Math.Max(2, Math.Min(blendOverlap, patchSize / 2));

        // Start with edge replicate as baseline
        using var baseReplicate = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, baseReplicate, padding.Top, padding.Bottom, padding.Left, padding.Right, BorderTypes.Replicate);

        int totalW = sourceBgr.Width + padding.Left + padding.Right;
        int totalH = sourceBgr.Height + padding.Top + padding.Bottom;

        var result = baseReplicate.Clone();

        int step = patchSize - blendOverlap;
        var rand = new Random(42);

        // Fill non-interior blocks by sampling random patches from near the inner border
        for (int y = 0; y < totalH - patchSize; y += step)
        {
            if (y % 16 == 0) ct.ThrowIfCancellationRequested();
            for (int x = 0; x < totalW - patchSize; x += step)
            {
                // If completely inside interior, skip
                if (x >= padding.Left && x + patchSize <= padding.Left + sourceBgr.Width &&
                    y >= padding.Top && y + patchSize <= padding.Top + sourceBgr.Height)
                {
                    continue;
                }

                // Sample patch from source interior near the closest edge
                int sampleX = rand.Next(0, Math.Max(1, sourceBgr.Width - patchSize));
                int sampleY = rand.Next(0, Math.Max(1, sourceBgr.Height - patchSize));

                // Prefer sampling from outer border region of source
                if (x < padding.Left) sampleX = rand.Next(0, Math.Min(sourceBgr.Width - patchSize, Math.Max(1, sourceBgr.Width / 3)));
                else if (x >= padding.Left + sourceBgr.Width) sampleX = rand.Next(Math.Max(0, sourceBgr.Width * 2 / 3 - patchSize), Math.Max(1, sourceBgr.Width - patchSize));

                if (y < padding.Top) sampleY = rand.Next(0, Math.Min(sourceBgr.Height - patchSize, Math.Max(1, sourceBgr.Height / 3)));
                else if (y >= padding.Top + sourceBgr.Height) sampleY = rand.Next(Math.Max(0, sourceBgr.Height * 2 / 3 - patchSize), Math.Max(1, sourceBgr.Height - patchSize));

                using var patch = new Mat(sourceBgr, new Rect(sampleX, sampleY, patchSize, patchSize));
                using var destRoi = new Mat(result, new Rect(x, y, patchSize, patchSize));

                // Feathered blend into destination ROI
                Cv2.AddWeighted(patch, 0.7, destRoi, 0.3, 0, destRoi);
            }
        }

        ct.ThrowIfCancellationRequested();

        // Restore interior
        if (blendMargin <= 0)
        {
            using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
        }
        else
        {
            BlendInteriorWithFeather(result, sourceBgr, padding, blendMargin, ct);
        }

        return result;
    }

    private static Mat FillSolidColorBlurred(Mat sourceBgr, CanvasPadding padding, int blurRadius, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var replicated = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, replicated, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Replicate);

        int kernel;
        if (blurRadius > 0)
        {
            kernel = blurRadius % 2 == 0 ? blurRadius + 1 : blurRadius;
            kernel = Math.Max(3, kernel);
        }
        else
        {
            int maxPad = Math.Max(Math.Max(padding.Left, padding.Right), Math.Max(padding.Top, padding.Bottom));
            kernel = Math.Max(3, (maxPad / 2) | 1);
        }

        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        Cv2.GaussianBlur(replicated, result, new Size(kernel, kernel), 0);

        ct.ThrowIfCancellationRequested();
        using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
        sourceBgr.CopyTo(interiorRoi);
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

        int dx = 0;
        if (x < intLeft) dx = intLeft - x;
        else if (x > intRight) dx = x - intRight;

        int dy = 0;
        if (y < intTop) dy = intTop - y;
        else if (y > intBottom) dy = y - intBottom;

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

        unsafe
        {
            byte* ptr = (byte*)canvas.DataPointer;
            long step = canvas.Step();

            for (int y = 0; y < h; y++)
            {
                if (y % 32 == 0) ct.ThrowIfCancellationRequested();
                for (int x = 0; x < w; x++)
                {
                    if (x >= intLeft && x < intRight && y >= intTop && y < intBottom)
                    {
                        continue;
                    }

                    int dx = 0;
                    if (x < intLeft) dx = intLeft - x;
                    else if (x >= intRight) dx = x - (intRight - 1);

                    int dy = 0;
                    if (y < intTop) dy = intTop - y;
                    else if (y >= intBottom) dy = y - (intBottom - 1);

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float t = Math.Clamp(dist / maxDist, 0f, 1f);
                    float multiplier = 1.0f - (1.0f - fadeFactor) * t;

                    byte* pixel = ptr + y * step + x * 3;
                    pixel[0] = (byte)Math.Clamp(pixel[0] * multiplier, 0, 255);
                    pixel[1] = (byte)Math.Clamp(pixel[1] * multiplier, 0, 255);
                    pixel[2] = (byte)Math.Clamp(pixel[2] * multiplier, 0, 255);
                }
            }
        }
    }

    private static void BlendInteriorWithFeather(Mat resultCanvas, Mat sourceBgr, CanvasPadding padding, int featherPx, CancellationToken ct)
    {
        int w = sourceBgr.Width;
        int h = sourceBgr.Height;
        featherPx = Math.Max(1, Math.Min(featherPx, Math.Min(w, h) / 4));

        ct.ThrowIfCancellationRequested();
        using var alphaMask = new Mat(h, w, MatType.CV_32FC1, Scalar.All(1.0));
        for (int y = 0; y < h; y++)
        {
            if (y % 16 == 0) ct.ThrowIfCancellationRequested();
            for (int x = 0; x < w; x++)
            {
                int distLeft = x;
                int distRight = w - 1 - x;
                int distTop = y;
                int distBottom = h - 1 - y;

                int minDist = int.MaxValue;
                if (padding.Left > 0) minDist = Math.Min(minDist, distLeft);
                if (padding.Right > 0) minDist = Math.Min(minDist, distRight);
                if (padding.Top > 0) minDist = Math.Min(minDist, distTop);
                if (padding.Bottom > 0) minDist = Math.Min(minDist, distBottom);

                if (minDist < featherPx)
                {
                    float factor = (float)minDist / featherPx;
                    alphaMask.Set(y, x, factor);
                }
            }
        }

        ct.ThrowIfCancellationRequested();
        using var interiorRoi = new Mat(resultCanvas, new Rect(padding.Left, padding.Top, w, h));
        using var source32F = new Mat();
        using var interior32F = new Mat();
        sourceBgr.ConvertTo(source32F, MatType.CV_32FC3);
        interiorRoi.ConvertTo(interior32F, MatType.CV_32FC3);

        using var alpha3 = new Mat();
        Cv2.CvtColor(alphaMask, alpha3, ColorConversionCodes.GRAY2BGR);

        using var ones3 = new Mat(alpha3.Size(), MatType.CV_32FC3, Scalar.All(1.0));
        using var invAlpha = new Mat();
        Cv2.Subtract(ones3, alpha3, invAlpha);

        using var blendedSource = new Mat();
        using var blendedInterior = new Mat();
        Cv2.Multiply(source32F, alpha3, blendedSource);
        Cv2.Multiply(interior32F, invAlpha, blendedInterior);

        using var blendedTotal = new Mat();
        Cv2.Add(blendedSource, blendedInterior, blendedTotal);
        blendedTotal.ConvertTo(interiorRoi, MatType.CV_8UC3);
    }
}

