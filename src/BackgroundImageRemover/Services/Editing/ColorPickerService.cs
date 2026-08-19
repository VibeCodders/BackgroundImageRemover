using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Samples pixel colors from a BGR image at a given coordinate.</summary>
public static class ColorPickerService
{
    /// <summary>Returns the BGR color at <paramref name="x"/>, <paramref name="y"/> (clamped to image bounds).</summary>
    public static Vec3b Sample(Mat bgr, int x, int y)
    {
        x = Math.Clamp(x, 0, bgr.Width - 1);
        y = Math.Clamp(y, 0, bgr.Height - 1);
        return bgr.At<Vec3b>(y, x);
    }

    /// <summary>Returns the average BGR color within a square region of the given <paramref name="radius"/> centered on (x, y).</summary>
    public static Vec3b SampleAverage(Mat bgr, int x, int y, int radius)
    {
        int r = Math.Max(1, radius);
        int x0 = Math.Clamp(x - r, 0, bgr.Width - 1);
        int y0 = Math.Clamp(y - r, 0, bgr.Height - 1);
        int x1 = Math.Clamp(x + r, 0, bgr.Width - 1);
        int y1 = Math.Clamp(y + r, 0, bgr.Height - 1);

        using var roi = new Mat(bgr, new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        var mean = Cv2.Mean(roi);
        return new Vec3b((byte)mean.Val0, (byte)mean.Val1, (byte)mean.Val2);
    }

    /// <summary>Converts a BGR vector to a CSS-style hex string (e.g. "#FF8040").</summary>
    public static string ToHex(Vec3b bgr)
    {
        // BGR -> RGB for display
        return $"#{bgr.Item2:X2}{bgr.Item1:X2}{bgr.Item0:X2}";
    }

    /// <summary>Returns the HSV representation of a BGR color.</summary>
    public static (double H, double S, double V) ToHsv(Vec3b bgr)
    {
        using var bgrMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(bgr.Item0, bgr.Item1, bgr.Item2));
        using var hsvMat = new Mat();
        Cv2.CvtColor(bgrMat, hsvMat, ColorConversionCodes.BGR2HSV);
        var hsv = hsvMat.At<Vec3b>(0, 0);
        return (hsv.Item0 * 2.0, hsv.Item1 / 255.0 * 100.0, hsv.Item2 / 255.0 * 100.0);
    }
}
