using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Resizing helpers. Each method returns a new Mat.</summary>
public static class ResizeService
{
    public static Mat ResizeTo(Mat src, int width, int height, ResampleMethod method = ResampleMethod.Lanczos)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var result = new Mat();
        Cv2.Resize(src, result, new Size(width, height), interpolation: ToFlags(method));
        return result;
    }

    public static Mat ResizePercent(Mat src, double percent, ResampleMethod method = ResampleMethod.Lanczos)
    {
        percent = Math.Max(0.01, percent);
        var result = new Mat();
        Cv2.Resize(src, result, new Size(0, 0), percent, percent, ToFlags(method));
        return result;
    }

    public static Mat ResizeToWidth(Mat src, int width, ResampleMethod method = ResampleMethod.Lanczos)
    {
        width = Math.Max(1, width);
        int height = Math.Max(1, (int)Math.Round((double)src.Height * width / src.Width));
        return ResizeTo(src, width, height, method);
    }

    public static Mat ResizeToHeight(Mat src, int height, ResampleMethod method = ResampleMethod.Lanczos)
    {
        height = Math.Max(1, height);
        int width = Math.Max(1, (int)Math.Round((double)src.Width * height / src.Height));
        return ResizeTo(src, width, height, method);
    }

    /// <summary>Scales to the largest size that fits inside the given box while preserving aspect ratio.</summary>
    public static Mat FitWithin(Mat src, int maxWidth, int maxHeight, ResampleMethod method = ResampleMethod.Lanczos)
    {
        maxWidth = Math.Max(1, maxWidth);
        maxHeight = Math.Max(1, maxHeight);
        double scale = Math.Min((double)maxWidth / src.Width, (double)maxHeight / src.Height);
        return ResizePercent(src, scale, method);
    }

    /// <summary>Scales to cover the given box (cropping overflow) while preserving aspect ratio.</summary>
    public static Mat FillTo(Mat src, int width, int height, ResampleMethod method = ResampleMethod.Lanczos)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        double scale = Math.Max((double)width / src.Width, (double)height / src.Height);
        using var scaled = ResizePercent(src, scale, method);
        var rect = CropService.CenteredRectForSize(scaled.Size(), width, height);
        return CropService.CropRect(scaled, rect);
    }

    /// <summary>Resizes so the longest side reaches the requested length, preserving aspect ratio.</summary>
    public static Mat ResizeToLongestSide(Mat src, int longest, ResampleMethod method = ResampleMethod.Lanczos)
    {
        longest = Math.Max(1, longest);
        return src.Width >= src.Height
            ? ResizeToWidth(src, longest, method)
            : ResizeToHeight(src, longest, method);
    }

    /// <summary>Resizes to the requested megapixel count (area), preserving aspect ratio.</summary>
    public static Mat ResizeToMegapixels(Mat src, double megapixels, ResampleMethod method = ResampleMethod.Lanczos)
    {
        megapixels = Math.Max(0.01, megapixels);
        double scale = Math.Sqrt((megapixels * 1_000_000.0) / (src.Width * (double)src.Height));
        return ResizePercent(src, scale, method);
    }

    private static InterpolationFlags ToFlags(ResampleMethod method)
        => ResampleMethodHelper.ToInterpolationFlags(method);
}
