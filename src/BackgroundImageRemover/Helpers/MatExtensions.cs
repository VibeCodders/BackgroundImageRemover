using System.Threading.Tasks;
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
    /// are assumed to already be in the 0..1 range. <paramref name="original"/> is expected to be
    /// 3-channel BGR (CV_8UC3); <paramref name="modified"/> may be CV_8UC3 or CV_32FC3 with
    /// values on the 0..255 scale (e.g. VignetteService passes a scaled float overlay).
    /// </summary>
    /// <remarks>
    /// Single-pass implementation over zero-copy <see cref="Span2D{T}"/> views: the previous
    /// version materialized ~7 intermediate CV_32FC3 Mats (a full-image float pass each) per call,
    /// which this is used by (blur/sharpen/dodge-burn/gradient/vignette/shape/tilt-shift) on every
    /// preview and apply. Blend math is identical: <c>result = original*(1-m) + modified*m</c>.
    /// </remarks>
    public static Mat BlendByMask(this Mat original, Mat modified, Mat mask)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(modified);
        ArgumentNullException.ThrowIfNull(mask);

        bool modFloat = modified.Type() == MatType.CV_32FC3;
        bool byteMask = mask.Type() == MatType.CV_8UC1;
        int rows = original.Rows;
        int cols = original.Cols;

        var result = new Mat(original.Size(), MatType.CV_8UC3);
        unsafe
        {
            byte* origPtr = (byte*)original.DataPointer;
            byte* modPtr = (byte*)modified.DataPointer;
            byte* maskPtr = (byte*)mask.DataPointer;
            byte* dstPtr = (byte*)result.DataPointer;
            long origStep = original.Step();
            long modStep = modified.Step();
            long maskStep = mask.Step();
            long dstStep = result.Step();

            // Rows are independent and each is processed by exactly one thread, so results are
            // identical to a sequential pass (same per-pixel math, no cross-row dependencies).
            Parallel.For(0, rows, y =>
            {
                var origRow = new Span<Vec3b>((Vec3b*)(origPtr + y * origStep), cols);
                var dstRow = new Span<Vec3b>((Vec3b*)(dstPtr + y * dstStep), cols);
                var maskByteRow = byteMask ? new Span<byte>((byte*)(maskPtr + y * maskStep), cols) : Span<byte>.Empty;
                var maskFloatRow = byteMask ? Span<float>.Empty : new Span<float>((float*)(maskPtr + y * maskStep), cols);
                var modByteRow = modFloat ? Span<Vec3b>.Empty : new Span<Vec3b>((Vec3b*)(modPtr + y * modStep), cols);
                var modFloatRow = modFloat ? new Span<Vec3f>((Vec3f*)(modPtr + y * modStep), cols) : Span<Vec3f>.Empty;
                for (int x = 0; x < cols; x++)
                {
                    float m = byteMask ? maskByteRow[x] / 255f : maskFloatRow[x];
                    float inv = 1f - m;
                    var orig = origRow[x];
                    float mb, mg, mr;
                    if (modFloat)
                    {
                        var mod = modFloatRow[x];
                        mb = mod.Item0;
                        mg = mod.Item1;
                        mr = mod.Item2;
                    }
                    else
                    {
                        var mod = modByteRow[x];
                        mb = mod.Item0;
                        mg = mod.Item1;
                        mr = mod.Item2;
                    }
                    dstRow[x] = new Vec3b(
                        BlendByte(orig.Item0, mb, inv, m),
                        BlendByte(orig.Item1, mg, inv, m),
                        BlendByte(orig.Item2, mr, inv, m));
                }
            });
        }
        return result;
    }

    private static byte BlendByte(byte a, float b, float inv, float m)
    {
        float v = a * inv + b * m;
        return (byte)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
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
