using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Cropping helpers on a BGR (or any-channel) image. Each method returns a new Mat.</summary>
public static class CropService
{
    /// <summary>Crops to the given rectangle, clamped to the image bounds. Returns a clone.</summary>
    public static Mat CropRect(Mat src, Rect rect)
    {
        var clamped = GeometryHelper.ClampToSize(src.Size(), rect);
        using var roi = new Mat(src, clamped);
        return roi.Clone();
    }

    /// <summary>Returns the largest centered rectangle with the requested aspect ratio (width/height).</summary>
    public static Rect CenteredRectForAspect(Size size, double ratio)
    {
        if (size.Width <= 0 || size.Height <= 0 || ratio <= 0)
        {
            return new Rect(0, 0, size.Width, size.Height);
        }

        double currentRatio = (double)size.Width / size.Height;
        int width;
        int height;
        if (ratio > currentRatio)
        {
            width = size.Width;
            height = (int)Math.Round(width / ratio);
        }
        else
        {
            height = size.Height;
            width = (int)Math.Round(height * ratio);
        }

        width = Math.Clamp(width, 1, size.Width);
        height = Math.Clamp(height, 1, size.Height);
        int x = (size.Width - width) / 2;
        int y = (size.Height - height) / 2;
        return new Rect(x, y, width, height);
    }

    /// <summary>Crops away the given margins (in pixels) from each side. An empty source is
    /// returned unchanged instead of crashing on degenerate bounds.</summary>
    public static Mat CropMargins(Mat src, int left, int top, int right, int bottom)
    {
        if (src.Width <= 0 || src.Height <= 0)
        {
            return src.Clone();
        }

        int width = Math.Max(1, src.Width - left - right);
        int height = Math.Max(1, src.Height - top - bottom);
        int x = Math.Clamp(left, 0, src.Width - 1);
        int y = Math.Clamp(top, 0, src.Height - 1);
        width = Math.Min(width, src.Width - x);
        height = Math.Min(height, src.Height - y);
        return CropRect(src, new Rect(x, y, width, height));
    }

    /// <summary>
    /// Computes the bounding box of the non-border content (the border color is sampled from
    /// the corners). Useful for removing letterbox bars or a flat backdrop.
    /// </summary>
    public static Rect TrimContentBounds(Mat src, int tolerance = 12)
    {
        if (src.Width <= 2 || src.Height <= 2)
        {
            return new Rect(0, 0, src.Width, src.Height);
        }

        var bg = src.At<Vec3b>(0, 0);
        return ComputeTrimBounds(src, bg, tolerance);
    }

    /// <summary>Computes the content bounding box assuming the given flat border color.</summary>
    public static Rect TrimContentBounds(Mat src, Vec3b borderColor, int tolerance)
    {
        if (src.Width <= 2 || src.Height <= 2)
        {
            return new Rect(0, 0, src.Width, src.Height);
        }

        return ComputeTrimBounds(src, borderColor, tolerance);
    }

    /// <summary>Returns the centered rectangle of the requested pixel size, clamped to the image bounds.</summary>
    public static Rect CenteredRectForSize(Size size, int width, int height)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        width = Math.Clamp(width, 1, size.Width);
        height = Math.Clamp(height, 1, size.Height);
        int x = (size.Width - width) / 2;
        int y = (size.Height - height) / 2;
        return new Rect(x, y, width, height);
    }

    private static Rect ComputeTrimBounds(Mat src, Vec3b bg, int tolerance)
    {
        Mat? converted = null;
        Mat bgr = src;
        try
        {
            if (src.Channels() == 4)
            {
                converted = ToBgr(src);
                bgr = converted;
            }

            using var diff = new Mat();
            Cv2.Absdiff(bgr, new Mat(bgr.Size(), bgr.Type(), new Scalar(bg.Item0, bg.Item1, bg.Item2)), diff);

            using var gray = new Mat();
            Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
            using var mask = new Mat();
            Cv2.Threshold(gray, mask, tolerance, 255, ThresholdTypes.Binary); // non-border = 255

            using var nonZero = new Mat();
            Cv2.FindNonZero(mask, nonZero);
            if (nonZero.Rows == 0)
            {
                return new Rect(0, 0, src.Width, src.Height);
            }

            return Cv2.BoundingRect(nonZero);
        }
        finally
        {
            converted?.Dispose();
        }
    }

    private static Mat ToBgr(Mat src)
    {
        var channels = Cv2.Split(src);
        try
        {
            var bgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
            return bgr;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>
    /// Trims a near-uniform border (the dominant corner color) off the image. Useful for
    /// removing letterbox bars or a flat backdrop.
    /// </summary>
    public static Mat TrimContent(Mat src, int tolerance = 12)
        => CropRect(src, TrimContentBounds(src, tolerance));

    /// <summary>Trims a border of the given color off the image.</summary>
    public static Mat TrimContent(Mat src, Vec3b borderColor, int tolerance = 12)
        => CropRect(src, TrimContentBounds(src, borderColor, tolerance));
}
