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

    private static InterpolationFlags ToFlags(ResampleMethod method) => method switch
    {
        ResampleMethod.Nearest => InterpolationFlags.Nearest,
        ResampleMethod.Linear => InterpolationFlags.Linear,
        ResampleMethod.Cubic => InterpolationFlags.Cubic,
        _ => InterpolationFlags.Lanczos4
    };
}
