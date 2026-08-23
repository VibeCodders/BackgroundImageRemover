using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
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
    /// Returns a clone of <paramref name="source"/>, or an empty Mat when the source is null,
    /// disposed or empty. Eliminates the repeated <c>if (mat is null || mat.Empty())</c> guard
    /// boilerplate scattered across the editing services.
    /// </summary>
    public static Mat CloneOrEmpty(this Mat? source)
    {
        if (source is null || source.IsDisposed || source.Empty())
        {
            return new Mat();
        }
        return source.Clone();
    }

    /// <summary>
    /// Returns <paramref name="source"/> when it is non-null and non-empty, otherwise an empty Mat.
    /// Unlike <see cref="CloneOrEmpty"/> the original (non-empty) instance is returned without copying,
    /// which is handy for the "no-op" fast path of an effect.
    /// </summary>
    public static Mat OrEmpty(this Mat? source)
    {
        if (source is null || source.IsDisposed || source.Empty())
        {
            return new Mat();
        }
        return source;
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
    /// Converts a Mat to a <see cref="System.Windows.Media.Imaging.BitmapSource"/> and freezes
    /// it. Frozen bitmaps render faster in WPF (the render thread can cache them without
    /// dispatcher-affinity checks) and are safe to hand to background threads. The source is
    /// always freshly created here, so freezing cannot break later mutation.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource ToFrozenBitmapSource(this Mat mat)
    {
        ArgumentNullException.ThrowIfNull(mat);
        var bitmap = mat.ToBitmapSource();
        bitmap.Freeze();
        return bitmap;
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
        return bgra.ToFrozenBitmapSource();
    }

    /// <summary>
    /// Builds the display bitmap for a preview-resolution BGR Mat, showing the full-resolution
    /// alpha channel when it carries meaningful transparency (a real cutout) and rendering the
    /// plain BGR otherwise. Eliminates the repeated
    /// <c>isCutout ? BuildPreviewWithAlpha : ToBitmapSource</c> ternary scattered across the
    /// document load/duplicate/project/rotate/uncrop flows and tool-session initialization.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource ToPreviewBitmap(this Mat previewBgr, Mat? fullAlpha)
    {
        ArgumentNullException.ThrowIfNull(previewBgr);
        if (fullAlpha is not null && BackgroundCompositingService.HasMeaningfulTransparency(fullAlpha))
        {
            return previewBgr.BuildPreviewWithAlpha(fullAlpha);
        }
        return previewBgr.ToFrozenBitmapSource();
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
        return bgra.ToFrozenBitmapSource();
    }

    /// <summary>
    /// Renders a BGR + alpha working pair into a <see cref="System.Windows.Media.Imaging.BitmapSource"/>,
    /// or null when either Mat is missing. Shared "reconstruct the result bitmap from the working
    /// pair" flow: the document's <c>RefreshResultBitmapFromWorking</c> and every tool-session
    /// <c>RefreshResult</c>/<c>RefreshPreview</c> use it instead of re-checking nullability.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource? ToResultBitmap(this Mat? bgr, Mat? alpha)
    {
        if (bgr is null || alpha is null)
        {
            return null;
        }
        return bgr.ToBitmapSource(alpha);
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

    /// <summary>
    /// Linearly interpolates (blends) between <paramref name="original"/> and <paramref name="modified"/>
    /// using a single-channel grayscale <paramref name="mask"/>. White pixels in the mask yield
    /// <paramref name="modified"/>, black pixels yield <paramref name="original"/>, and intermediate
    /// values produce a proportional blend. Byte masks are normalized 0..255 → 0..1; float masks
    /// are assumed to already be in the 0..1 range. Both inputs are expected to be 3-channel BGR
    /// at the same size.
    /// </summary>
    public static Mat BlendByMask(this Mat original, Mat modified, Mat mask)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(modified);
        ArgumentNullException.ThrowIfNull(mask);

        using var maskF = new Mat();
        if (mask.Type() == MatType.CV_8UC1)
        {
            mask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);
        }
        else
        {
            mask.ConvertTo(maskF, MatType.CV_32FC1);
        }

        using var mask3 = new Mat();
        Cv2.CvtColor(maskF, mask3, ColorConversionCodes.GRAY2BGR);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(mask3.Size(), mask3.Type(), Scalar.All(1.0)), mask3, inv);

        using var aF = new Mat();
        original.ConvertTo(aF, MatType.CV_32FC3);
        using var bF = new Mat();
        modified.ConvertTo(bF, MatType.CV_32FC3);

        using var aWeighted = aF.Mul(inv).ToMat();
        using var bWeighted = bF.Mul(mask3).ToMat();
        using var blended = (aWeighted + bWeighted).ToMat();

        var result = new Mat();
        blended.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }

    /// <summary>
    /// Returns the alpha channel from a <see cref="LoadedImage"/> as an independent, full-resolution
    /// <see cref="Mat"/>. When the image has no alpha, a fully-opaque (255) single-channel Mat of the
    /// same size as <see cref="LoadedImage.FullBgr"/> is created instead.
    /// </summary>
    public static Mat GetWorkingAlpha(this LoadedImage? image)
    {
        if (image is null)
        {
            return new Mat();
        }

        return image.FullAlpha?.Clone()
            ?? new Mat(image.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
    }
}
