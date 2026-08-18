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

    public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        var borderType = mirrorType == UncropMirrorType.Reflect ? BorderTypes.Reflect : BorderTypes.Reflect101;
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right, borderType);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var expanded = ExpandCanvas(sourceBgr, padding, out var mask);
        using (mask)
        {
            ct.ThrowIfCancellationRequested();
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

    public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
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

    public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Replicate);
        ct.ThrowIfCancellationRequested();
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
            kernel = Math.Max(3, (maxPad / 2) | 1); // odd kernel size, scaled with the padding
        }

        ct.ThrowIfCancellationRequested();
        var result = new Mat();
        Cv2.GaussianBlur(replicated, result, new Size(kernel, kernel), 0);

        ct.ThrowIfCancellationRequested();
        using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
        sourceBgr.CopyTo(interiorRoi);
        return result;
    }

    /// <summary>Average BGR color of a thin band along the image's outer edge.</summary>
    private static Scalar SampleEdgeAverageColor(Mat sourceBgr)
    {
        int band = Math.Max(1, Math.Min(5, Math.Min(sourceBgr.Width, sourceBgr.Height) / 4));
        using var mask = new Mat(sourceBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Rect(0, 0, sourceBgr.Width, sourceBgr.Height), Scalar.All(255), thickness: band);
        return Cv2.Mean(sourceBgr, mask);
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

    private static void BlendInteriorWithFeather(Mat resultCanvas, Mat sourceBgr, CanvasPadding padding, int featherPx, CancellationToken ct)
    {
        int w = sourceBgr.Width;
        int h = sourceBgr.Height;
        featherPx = Math.Max(1, Math.Min(featherPx, Math.Min(w, h) / 4));

        ct.ThrowIfCancellationRequested();
        using var alphaMask = new Mat(h, w, MatType.CV_32FC1, Scalar.All(1.0));
        // Soft gradient around outer border of interior
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
