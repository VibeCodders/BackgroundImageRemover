using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Censorship effects (pixelation / blur) applied to a region or the whole image.</summary>
public static class MosaicService
{
    /// <summary>Pixelates a region (or the whole image when <paramref name="region"/> is null) with the given cell size.</summary>
    public static Mat Pixelate(Mat src, Rect? region, int cellSize)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        cellSize = Math.Max(1, cellSize);

        int smallW = Math.Max(1, bounds.Width / cellSize);
        int smallH = Math.Max(1, bounds.Height / cellSize);

        using var roi = new Mat(result, bounds);
        using var small = new Mat();
        Cv2.Resize(roi, small, new Size(smallW, smallH), interpolation: InterpolationFlags.Area);
        Cv2.Resize(small, roi, bounds.Size, interpolation: InterpolationFlags.Nearest);
        return result;
    }

    /// <summary>Blurs a region (or the whole image when <paramref name="region"/> is null) with the given radius.</summary>
    public static Mat Blur(Mat src, Rect? region, int radius)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        radius = Math.Max(1, radius);

        using var roi = new Mat(result, bounds);
        Cv2.GaussianBlur(roi, roi, new Size(0, 0), radius, radius);
        return result;
    }

    private static Rect Clamp(Size size, Rect rect)
    {
        int x = Math.Clamp(rect.X, 0, size.Width - 1);
        int y = Math.Clamp(rect.Y, 0, size.Height - 1);
        int width = Math.Clamp(rect.Width, 1, size.Width - x);
        int height = Math.Clamp(rect.Height, 1, size.Height - y);
        return new Rect(x, y, width, height);
    }
}
