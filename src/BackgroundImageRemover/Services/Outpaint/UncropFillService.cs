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

    public Mat FillMirror(Mat sourceBgr, CanvasPadding padding)
    {
        var result = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, result, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Reflect101);
        return result;
    }

    public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method)
    {
        using var expanded = ExpandCanvas(sourceBgr, padding, out var mask);
        using (mask)
        {
            var result = new Mat();
            var cvMethod = method == UncropInpaintMethod.Telea ? InpaintMethod.Telea : InpaintMethod.NS;
            // A large inpaintRadius on a wide, freshly-added border can look smeared since
            // Cv2.Inpaint is designed for filling thin damaged regions, not generating new
            // content -- an acceptable classic-algorithm limitation for this fill mode.
            Cv2.Inpaint(expanded, mask, result, inpaintRadius: 5, cvMethod);
            return result;
        }
    }

    public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred)
    {
        if (padding.IsZero)
        {
            return sourceBgr.Clone();
        }

        if (blurred)
        {
            return FillSolidColorBlurred(sourceBgr, padding);
        }

        var expanded = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Constant, Scalar.All(0));
        var edgeColor = SampleEdgeAverageColor(sourceBgr);
        FillBorderRegions(expanded, padding, sourceBgr.Size(), edgeColor);
        return expanded;
    }

    private static Mat FillSolidColorBlurred(Mat sourceBgr, CanvasPadding padding)
    {
        // Stretch the outermost edge pixels outward (so the fill continues the image's local
        // color/texture instead of a flat tone), then blur that stretched border into a soft
        // gradient. The blur is confined to the border by restoring the original, unblurred
        // pixels back into the interior afterwards.
        using var replicated = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, replicated, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Replicate);

        int maxPad = Math.Max(Math.Max(padding.Left, padding.Right), Math.Max(padding.Top, padding.Bottom));
        int kernel = Math.Max(3, (maxPad / 2) | 1); // odd kernel size, scaled with the padding

        var result = new Mat();
        Cv2.GaussianBlur(replicated, result, new Size(kernel, kernel), 0);

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
}
