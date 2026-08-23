using BackgroundImageRemover.Helpers;
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
        var borderType = mirrorType == UncropMirrorType.Reflect ? BorderTypes.Reflect : BorderTypes.Reflect101;
        var result = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, borderType);
        ct.ThrowIfCancellationRequested();

        if (blurRadius > 0)
        {
            var blurred = ImageProcessingUtility.BlurBorderAndRestoreInterior(result, sourceBgr, padding,
                ImageProcessingUtility.OddKernelAtLeast(blurRadius), ct);
            result.Dispose();
            result = blurred;
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

        var expanded = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, BorderTypes.Constant, Scalar.All(0));
        var color = customColor ?? SampleEdgeAverageColor(sourceBgr);
        ct.ThrowIfCancellationRequested();
        FillBorderRegions(expanded, padding, sourceBgr.Size(), color);
        ct.ThrowIfCancellationRequested();
        return expanded;
    }

    public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, BorderTypes.Replicate);
        ct.ThrowIfCancellationRequested();

        if (smoothRadius > 0)
        {
            var smoothed = ImageProcessingUtility.BlurBorderAndRestoreInterior(result, sourceBgr, padding,
                ImageProcessingUtility.OddKernelAtLeast(smoothRadius), ct);
            result.Dispose();
            result = smoothed;
        }

        return result;
    }

    public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, BorderTypes.Wrap);
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
        var canvasSize = padding.ExpandedSize(sourceBgr.Size());
        int totalW = canvasSize.Width;
        int totalH = canvasSize.Height;

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
            ImageProcessingUtility.RestoreInterior(result, sourceBgr, padding);
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
        using var replicated = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, BorderTypes.Replicate);

        int kernel = blurRadius > 0
            ? ImageProcessingUtility.OddKernelAtLeast(blurRadius)
            : Math.Max(3, (Math.Max(Math.Max(padding.Left, padding.Right), Math.Max(padding.Top, padding.Bottom)) / 2) | 1);

        ct.ThrowIfCancellationRequested();
        return ImageProcessingUtility.BlurBorderAndRestoreInterior(replicated, sourceBgr, padding, kernel, ct);
    }
}
