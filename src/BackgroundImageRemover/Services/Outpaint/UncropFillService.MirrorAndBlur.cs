using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

public sealed partial class UncropFillService
{
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
}
