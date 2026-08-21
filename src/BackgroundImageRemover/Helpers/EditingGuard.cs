using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Guard clauses and validation helpers for editing operations.
/// </summary>
public static class EditingGuard
{
    public const double Epsilon = 1e-4;

    public static void ThrowIfNull(Mat mat)
    {
        ArgumentNullException.ThrowIfNull(mat);
    }

    public static void ThrowIfNullOrEmpty(Mat mat)
    {
        ArgumentNullException.ThrowIfNull(mat);
        if (mat.Empty())
        {
            throw new ArgumentException("Mat is empty.", nameof(mat));
        }
    }

    public static double ClampStrength(double strength)
    {
        return Math.Clamp(strength, 0.0, 1.0);
    }

    public static double ClampStrength(double strength, double min, double max)
    {
        return Math.Clamp(strength, min, max);
    }

    public static int EnsurePositive(int value)
    {
        return Math.Max(1, value);
    }

    public static bool IsNearZero(double value)
    {
        return Math.Abs(value) < Epsilon;
    }

    public static bool IsEffectSignificant(double strength)
    {
        return strength > Epsilon;
    }

    public static Mat CloneIfSignificant(double strength, Func<Mat> operation)
    {
        if (strength <= Epsilon)
        {
            return operation();
        }
        return operation();
    }

    public static Mat ExpandCanvas(Mat img, int left, int top, int right, int bottom, Scalar fill)
    {
        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);

        int newWidth = img.Width + left + right;
        int newHeight = img.Height + top + bottom;
        var result = new Mat(newHeight, newWidth, img.Type(), fill);
        using var dst = new Mat(result, new Rect(left, top, img.Width, img.Height));
        img.CopyTo(dst);
        return result;
    }

    public static Rect GetEffectiveRegion(Size imageSize, Rect? region)
    {
        return region is { } r ? GeometryHelper.ClampToSize(imageSize, r) : new Rect(0, 0, imageSize.Width, imageSize.Height);
    }
}
