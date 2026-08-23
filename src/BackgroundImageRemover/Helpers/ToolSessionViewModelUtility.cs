using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Reusable extension methods and helpers for <see cref="ToolSessionViewModelBase"/> to eliminate
/// duplicated boilerplate across tool session view models.
/// </summary>
public static class ToolSessionViewModelUtility
{
    /// <summary>
    /// Converts a WPF <see cref="Color"/> to an OpenCV <see cref="Vec3b"/> in BGR order.
    /// </summary>
    public static Vec3b ToVec3b(this Color color)
    {
        return new Vec3b(color.B, color.G, color.R);
    }

    /// <summary>
    /// Attempts to build a <see cref="BitmapSource"/> from a result Mat and alpha Mat.
    /// Returns false if either Mat is null.
    /// </summary>
    public static bool TrySetResultBitmap(this ToolSessionViewModelBase tool, Mat? result, Mat? alpha, out BitmapSource? bitmap)
    {
        bitmap = null;
        if (result is null || alpha is null)
        {
            return false;
        }

        using var bgra = result.ToBgra(alpha);
        bitmap = bgra.ToFrozenBitmapSource();
        return true;
    }

    /// <summary>
    /// Safely chains a transformation on a Mat, disposing the previous Mat when the next
    /// operation returns a new instance. The <paramref name="owns"/> flag tracks whether the
    /// current Mat must be disposed before being replaced.
    /// </summary>
    public static Mat SafeChain(this Mat current, Func<Mat, Mat> next, ref bool owns)
    {
        var result = next(current);
        if (owns)
        {
            current.Dispose();
        }
        owns = true;
        return result;
    }

    /// <summary>
    /// Safely chains a transformation on a Mat with a try/catch guard that disposes the current
    /// Mat on failure before re-throwing.
    /// </summary>
    public static Mat SafeChainWithCatch(this Mat current, Func<Mat, Mat> next, ref bool owns)
    {
        try
        {
            return current.SafeChain(next, ref owns);
        }
        catch
        {
            if (owns)
            {
                current.Dispose();
            }
            throw;
        }
    }

    /// <summary>
    /// Returns true if any of the supplied boolean conditions are true.
    /// </summary>
    public static bool IsDirtyFrom(params bool[] conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the absolute difference between <paramref name="value"/> and
    /// <paramref name="baseline"/> exceeds <see cref="ImageProcessingUtility.Epsilon"/>.
    /// </summary>
    public static bool IsEffectSignificant(double value, double baseline = 0.0)
    {
        return ImageProcessingUtility.IsEffectSignificant(value - baseline);
    }
}
