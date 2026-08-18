using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Reusable extension methods on OpenCvSharp <see cref="Mat"/> to eliminate boilerplate and duplicate code.
/// </summary>
public static class MatExtensions
{
    /// <summary>
    /// Converts a BGR or BGRA Mat to a fresh BGRA Mat. If already BGRA, returns a clone.
    /// </summary>
    public static Mat ToBgra(this Mat source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Channels() == 4)
        {
            return source.Clone();
        }

        var result = new Mat();
        Cv2.CvtColor(source, result, ColorConversionCodes.BGR2BGRA);
        return result;
    }

    /// <summary>
    /// Converts a BGRA or BGR Mat to a fresh 3-channel BGR Mat. If already BGR, returns a clone.
    /// </summary>
    public static Mat ToBgr(this Mat source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Channels() == 3)
        {
            return source.Clone();
        }

        var result = new Mat();
        Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
        return result;
    }

    /// <summary>
    /// Composites a 3-channel BGR Mat with a single-channel alpha Mat into a fresh BGRA Mat,
    /// eliminating the repeated BGR→BGRA conversion + alpha-channel replacement boilerplate.
    /// </summary>
    public static Mat ToBgra(this Mat bgr, Mat alpha)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        ArgumentNullException.ThrowIfNull(alpha);

        var bgra = new Mat();
        Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        BackgroundImageRemover.Services.Compositing.BackgroundCompositingService.ReplaceAlphaChannel(bgra, alpha);
        return bgra;
    }

    /// <summary>
    /// Clones a Mat safely, returning null if the source is null or disposed.
    /// </summary>
    public static Mat? CloneSafe(this Mat? source)
    {
        if (source is null || source.IsDisposed)
        {
            return null;
        }
        return source.Clone();
    }

    /// <summary>
    /// Replaces the alpha channel of a 4-channel BGRA Mat in-place with a single-channel alpha Mat.
    /// </summary>
    public static void SetAlphaChannel(this Mat bgra, Mat alpha)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        ArgumentNullException.ThrowIfNull(alpha);

        if (bgra.Channels() != 4)
        {
            throw new ArgumentException("Source Mat must have 4 channels (BGRA).", nameof(bgra));
        }

        var channels = Cv2.Split(bgra);
        try
        {
            alpha.CopyTo(channels[3]);
            Cv2.Merge(channels, bgra);
        }
        finally
        {
            foreach (var ch in channels)
            {
                ch.Dispose();
            }
        }
    }

    /// <summary>
    /// Extracts the single-channel alpha mask from a 4-channel BGRA Mat.
    /// </summary>
    public static Mat ExtractAlphaChannel(this Mat bgra)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.Channels() != 4)
        {
            throw new ArgumentException("Source Mat must have 4 channels (BGRA).", nameof(bgra));
        }

        var channels = Cv2.Split(bgra);
        try
        {
            return channels[3].Clone();
        }
        finally
        {
            foreach (var ch in channels)
            {
                ch.Dispose();
            }
        }
    }

    /// <summary>
    /// Builds a preview-resolution BGRA BitmapSource from a preview BGR Mat plus a full-resolution alpha Mat.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource BuildPreviewWithAlpha(this OpenCvSharp.Mat previewBgr, OpenCvSharp.Mat fullAlpha)
    {
        ArgumentNullException.ThrowIfNull(previewBgr);
        ArgumentNullException.ThrowIfNull(fullAlpha);

        using var previewAlpha = new Mat();
        Cv2.Resize(fullAlpha, previewAlpha, previewBgr.Size(), interpolation: InterpolationFlags.Area);
        using var bgra = previewBgr.ToBgra(previewAlpha);
        return bgra.ToBitmapSource();
    }

    /// <summary>
    /// Composites a 3-channel BGR Mat with a single-channel alpha Mat into a BGRA
    /// <see cref="System.Windows.Media.Imaging.BitmapSource"/>, eliminating the repeated
    /// BGR→BGRA conversion + alpha-channel replacement boilerplate across ViewModels.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(this Mat bgr, Mat alpha)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        ArgumentNullException.ThrowIfNull(alpha);

        using var bgra = bgr.ToBgra(alpha);
        return bgra.ToBitmapSource();
    }

    /// <summary>
    /// Resizes a scribble mask Mat to a target size using nearest neighbor interpolation. Returns null if scribble is null.
    /// </summary>
    public static Mat? ResizeScribble(this Mat? scribble, Size targetSize)
    {
        if (scribble is null)
        {
            return null;
        }
        var resized = new Mat();
        Cv2.Resize(scribble, resized, targetSize, interpolation: InterpolationFlags.Nearest);
        return resized;
    }
}
