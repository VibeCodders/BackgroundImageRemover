using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Frame and border effects on a BGRA image (border, rounded corners, transparent padding).
/// Every method returns a new Mat.
/// </summary>
public static class FrameService
{
    /// <summary>Adds an opaque border of <paramref name="thickness"/> pixels around the image.</summary>
    public static Mat AddBorder(Mat bgra, int thickness, Vec3b color)
    {
        thickness = Math.Max(0, thickness);
        if (thickness == 0)
        {
            return bgra.Clone();
        }

        var result = new Mat(
            bgra.Height + 2 * thickness,
            bgra.Width + 2 * thickness,
            MatType.CV_8UC4,
            new Scalar(color.Item0, color.Item1, color.Item2, 255));

        using var inner = new Mat(result, new Rect(thickness, thickness, bgra.Width, bgra.Height));
        bgra.CopyTo(inner);
        return result;
    }

    /// <summary>Rounds the alpha channel (and the color of the removed corners) to a transparent radius.</summary>
    public static Mat RoundCorners(Mat bgra, int radius)
    {
        radius = Math.Max(0, Math.Min(radius, Math.Min(bgra.Width, bgra.Height) / 2));
        if (radius == 0)
        {
            return bgra.Clone();
        }

        using var mask = new Mat(bgra.Size(), MatType.CV_8UC1, Scalar.All(0));

        // Two overlapping rectangles plus four corner discs build a filled rounded rectangle.
        Cv2.Rectangle(mask, new Rect(0, radius, bgra.Width, bgra.Height - 2 * radius), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Rect(radius, 0, bgra.Width - 2 * radius, bgra.Height), Scalar.All(255), -1);
        Cv2.Circle(mask, radius, radius, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, bgra.Width - radius - 1, radius, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, radius, bgra.Height - radius - 1, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, bgra.Width - radius - 1, bgra.Height - radius - 1, radius, Scalar.All(255), -1);

        var result = bgra.Clone();
        using var inverted = new Mat();
        Cv2.BitwiseNot(mask, inverted);
        result.SetTo(new Scalar(0, 0, 0, 0), inverted);
        return result;
    }

    /// <summary>Expands the canvas by transparent margins (useful for adding breathing room around a cutout).</summary>
    public static Mat AddPadding(Mat bgra, int top, int right, int bottom, int left)
    {
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);
        left = Math.Max(0, left);
        if (top + right + bottom + left == 0)
        {
            return bgra.Clone();
        }

        var result = new Mat(
            bgra.Height + top + bottom,
            bgra.Width + left + right,
            MatType.CV_8UC4,
            Scalar.All(0));

        using var inner = new Mat(result, new Rect(left, top, bgra.Width, bgra.Height));
        bgra.CopyTo(inner);
        return result;
    }
}
