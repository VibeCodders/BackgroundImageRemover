using System.Threading.Tasks;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Reusable image processing operations to eliminate boilerplate across editing services.
/// </summary>
public static class ImageProcessingUtility
{
    public const double Epsilon = 1e-4;
    public const double StrengthLowerBound = 0.0;
    public const double StrengthUpperBound = 1.0;

    /// <summary>
    /// Alpha-composites <paramref name="overlayBgra"/> over <paramref name="baseBgra"/>,
    /// scaled by <paramref name="opacity"/> (0..1). Single parallel pass over the native
    /// buffers: the previous version materialized ~11 intermediate CV_32F Mats. Blend math is
    /// identical (<c>result = base*(1-a) + overlay*a</c> with <c>a = overlayAlpha*opacity/255</c>)
    /// and the result alpha stays the base alpha.
    /// </summary>
    public static Mat CompositeOverBgra(Mat baseBgra, Mat overlayBgra, double opacity)
    {
        int cols = baseBgra.Cols;
        float op = (float)opacity;
        var result = new Mat(baseBgra.Size(), MatType.CV_8UC4);
        unsafe
        {
            PixelLoop.ForEachRowParallel(baseBgra, overlayBgra, result, (basePtr, ovPtr, dstPtr, _) =>
            {
                var baseRow = new Span<Vec4b>((Vec4b*)basePtr, cols);
                var ovRow = new Span<Vec4b>((Vec4b*)ovPtr, cols);
                var dstRow = new Span<Vec4b>((Vec4b*)dstPtr, cols);
                for (int x = 0; x < cols; x++)
                {
                    var b = baseRow[x];
                    var o = ovRow[x];
                    float a = o.Item3 * op / 255f;
                    float inv = 1f - a;
                    dstRow[x] = new Vec4b(
                        PixelColor.BlendWeighted(o.Item0, a, b.Item0, inv),
                        PixelColor.BlendWeighted(o.Item1, a, b.Item1, inv),
                        PixelColor.BlendWeighted(o.Item2, a, b.Item2, inv),
                        b.Item3);
                }
            });
        }
        return result;
    }

    /// <summary>
    /// Alpha-composites <paramref name="overlayRoi"/> onto <paramref name="dstRoi"/> in place,
    /// scaled by <paramref name="opacity"/> (0..1). Single parallel pass over the native
    /// buffers; the previous version materialized ~8 intermediate CV_32F Mats per call (text
    /// overlay and shape/mosaic stamping call it per block). Works on ROI views (the row
    /// pointers honor the parent's stride).
    /// </summary>
    public static Mat AlphaComposite(Mat dstRoi, Mat overlayRoi, double opacity)
    {
        int cols = dstRoi.Cols;
        float op = (float)opacity;
        unsafe
        {
            // In-place pass: the destination is also the base input, so the same Mat is passed twice.
            PixelLoop.ForEachRowParallel(dstRoi, overlayRoi, dstRoi, (dstPtr, ovPtr, _, _) =>
            {
                var dstRow = new Span<Vec3b>((Vec3b*)dstPtr, cols);
                var ovRow = new Span<Vec4b>((Vec4b*)ovPtr, cols);
                for (int x = 0; x < cols; x++)
                {
                    var d = dstRow[x];
                    var o = ovRow[x];
                    float a = o.Item3 * op / 255f;
                    float inv = 1f - a;
                    dstRow[x] = new Vec3b(
                        PixelColor.BlendWeighted(o.Item0, a, d.Item0, inv),
                        PixelColor.BlendWeighted(o.Item1, a, d.Item1, inv),
                        PixelColor.BlendWeighted(o.Item2, a, d.Item2, inv));
                }
            });
        }
        return dstRoi;
    }

    public static Mat BlendLinear(Mat a, Mat b, double t)
    {
        if (t <= 0.001)
        {
            return a.Clone();
        }
        if (t >= 0.999)
        {
            return b.Clone();
        }

        var result = new Mat();
        Cv2.AddWeighted(a, 1.0 - t, b, t, 0, result);
        return result;
    }

    /// <summary>
    /// Blends <paramref name="baseImage"/> toward the result of <paramref name="effect"/> by
    /// <paramref name="amount"/> (0..1) and disposes the input, returning the blended Mat.
    /// Ownership of <paramref name="baseImage"/> transfers to this method, so it must not be
    /// used afterwards. Eliminates the repeated "apply effect + AddWeighted + dispose input"
    /// boilerplate in pipelines (e.g. <see cref="ImageProcessingHelper.ApplyAdjustments"/>).
    /// </summary>
    public static Mat BlendInPlace(Mat baseImage, double amount, Func<Mat, Mat> effect)
    {
        using var effectImage = effect(baseImage);
        var result = new Mat();
        Cv2.AddWeighted(baseImage, 1.0 - amount, effectImage, amount, 0, result);
        baseImage.Dispose();
        return result;
    }

    public static Mat AdjustSaturation(Mat bgr, double boost)
    {
        return AdjustSaturationByMultiplier(bgr, 1.0 + boost);
    }

    /// <summary>Scales the saturation channel (HSV) by a multiplicative factor; 1.0 leaves the image unchanged.</summary>
    public static Mat AdjustSaturationByMultiplier(Mat bgr, double multiplier)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        using var split = ChannelSplit.Of(hsv);
        split[1].ConvertTo(split[1], MatType.CV_8UC1, multiplier);
        var result = new Mat();
        Cv2.Merge(split.Channels, result);
        using (result)
        {
            var output = new Mat();
            Cv2.CvtColor(result, output, ColorConversionCodes.HSV2BGR);
            return output;
        }
    }

    /// <summary>Applies CLAHE contrast equalization on the Lab L channel, returning a new BGR Mat.</summary>
    public static Mat ApplyClahe(Mat bgr) => ApplyClahe(bgr, clipLimit: 2.0, tileSize: 8);

    /// <summary>
    /// Applies CLAHE contrast equalization on the Lab L channel with explicit parameters,
    /// returning a new BGR Mat. Used by the shared pipelines and by
    /// <see cref="BackgroundImageRemover.Services.Editing.LevelsService.Equalize"/>.
    /// </summary>
    public static Mat ApplyClahe(Mat bgr, double clipLimit, int tileSize)
    {
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);
        var labChannels = Cv2.Split(lab);
        try
        {
            using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileSize, tileSize));
            clahe.Apply(labChannels[0], labChannels[0]);
            Cv2.Merge(labChannels, lab);
            var result = new Mat();
            Cv2.CvtColor(lab, result, ColorConversionCodes.Lab2BGR);
            return result;
        }
        finally
        {
            foreach (var ch in labChannels) ch.Dispose();
        }
    }

    /// <summary>
    /// Neutralizes color casts with a gray-world assumption: each channel is scaled so its mean
    /// matches the average of the three channel means. Gains are clamped to [0.5, 2.0] and a
    /// near-zero mean leaves that channel untouched, so degenerate inputs cannot blow up. Returns
    /// a new BGR Mat. Replaces the copy-pasted implementations in
    /// <see cref="BackgroundImageRemover.Services.Editing.LevelsService"/>,
    /// <see cref="BackgroundImageRemover.Services.Refinement.RetouchEffectsService"/> and
    /// <see cref="ImageProcessingHelper.ApplyAutoEnhance"/>.
    /// </summary>
    public static Mat AutoWhiteBalance(Mat bgr)
    {
        var channels = Cv2.Split(bgr);
        try
        {
            double[] means = new double[3];
            double avg = 0.0;
            for (int i = 0; i < 3; i++)
            {
                means[i] = Cv2.Mean(channels[i]).Val0;
                avg += means[i];
            }
            avg /= 3.0;

            for (int i = 0; i < 3; i++)
            {
                double gain = means[i] < 1e-3 ? 1.0 : avg / means[i];
                gain = Math.Clamp(gain, 0.5, 2.0);
                using var adjusted = new Mat();
                channels[i].ConvertTo(adjusted, MatType.CV_8UC1, gain, 0.0);
                adjusted.CopyTo(channels[i]);
            }

            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    public static Mat ToGrayBgr(this Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        var result = new Mat();
        Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    public static Mat BuildLut(ReadOnlySpan<byte> table)
    {
        var lutMat = new Mat(1, 256, MatType.CV_8UC1);
        lutMat.SetArray(table.ToArray());
        return lutMat;
    }

    public static Mat BuildPosterizeLut(int levels)
    {
        int bucket = Math.Max(1, 256 / Math.Max(1, levels));
        var lut = new byte[256];
        for (int i = 0; i < lut.Length; i++)
        {
            lut[i] = (byte)((i / bucket) * bucket);
        }
        return BuildLut(lut);
    }

    /// <summary>Converts a 0..1 opacity to a 0..255 alpha byte, clamping out-of-range input.</summary>
    public static byte OpacityToAlphaByte(double opacity)
        => (byte)Math.Round(255 * Math.Clamp(opacity, 0.0, 1.0));

    /// <summary>
    /// Converts an 8-bit single-channel Mat (alpha mask, grayscale, …) to CV_32FC1 with values in
    /// 0..1, ready to be used as a multiplicative weight. Replaces the copy-pasted
    /// <c>ConvertTo(…, MatType.CV_32FC1, 1.0 / 255.0)</c> normalization in the decontamination,
    /// compositing and overlay services.
    /// </summary>
    public static Mat Gray8ToFloat01(Mat gray8)
    {
        var result = new Mat();
        gray8.ConvertTo(result, MatType.CV_32FC1, 1.0 / 255.0);
        return result;
    }

    public static int GaussianKernelSize(double radius)
    {
        return Math.Max(1, (int)Math.Round(radius * 2) | 1);
    }

    public static bool IsEffectSignificant(double strength)
    {
        return strength > Epsilon;
    }

    public static Mat ApplyToRegion(Mat src, Rect? region, Action<Mat> regionOp)
    {
        var result = src.Clone();
        var bounds = region is { } r ? GeometryHelper.ClampToSize(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        using var roi = new Mat(result, bounds);
        regionOp(roi);
        return result;
    }

    /// <summary>Returns an odd kernel size of at least <paramref name="min"/>, rounding an even radius up by one.</summary>
    public static int OddKernelAtLeast(int radius, int min = 3)
        => Math.Max(min, radius % 2 == 0 ? radius + 1 : radius);

    /// <summary>
    /// Pads <paramref name="src"/> on all four sides with <paramref name="padding"/> using the
    /// given border mode (optionally with a constant <paramref name="borderValue"/>). Replaces the
    /// copy-pasted <c>Cv2.CopyMakeBorder</c> calls that each repeated the
    /// top/bottom/left/right padding order.
    /// </summary>
    public static Mat ExpandBorder(Mat src, CanvasPadding padding, BorderTypes borderType, Scalar? borderValue = null)
    {
        var dst = new Mat();
        if (borderValue is { } value)
        {
            Cv2.CopyMakeBorder(src, dst, padding.Top, padding.Bottom, padding.Left, padding.Right, borderType, value);
        }
        else
        {
            Cv2.CopyMakeBorder(src, dst, padding.Top, padding.Bottom, padding.Left, padding.Right, borderType);
        }

        return dst;
    }

    /// <summary>
    /// Builds a CV_8UC1 mask that is 255 over the padded (new) area of a canvas and 0 over the
    /// source interior — the "what did uncrop add" mask used by the outpaint services.
    /// </summary>
    public static Mat CreateNewAreaMask(Size canvasSize, CanvasPadding padding, Size sourceSize)
    {
        var mask = new Mat(canvasSize, MatType.CV_8UC1, Scalar.All(255));
        using (var innerRoi = new Mat(mask, padding.InteriorRect(sourceSize)))
        {
            innerRoi.SetTo(Scalar.All(0));
        }

        return mask;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into the interior (unpadded) region of
    /// <paramref name="canvas"/>, overwriting whatever the border fill produced there.
    /// </summary>
    public static void RestoreInterior(Mat canvas, Mat source, CanvasPadding padding)
    {
        using var interiorRoi = new Mat(canvas, padding.InteriorRect(source.Size()));
        source.CopyTo(interiorRoi);
    }

    /// <summary>
    /// Gaussian-blurs the whole <paramref name="canvas"/> with a kernel of
    /// <paramref name="kernel"/> and restores the untouched <paramref name="source"/> in the
    /// interior, returning a new Mat (the input is left intact). The "blur the padded border
    /// then put the crisp original back" step shared by the mirror/replicate/solid-color fills.
    /// </summary>
    public static Mat BlurBorderAndRestoreInterior(Mat canvas, Mat source, CanvasPadding padding, int kernel, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var blurred = new Mat();
        Cv2.GaussianBlur(canvas, blurred, new Size(kernel, kernel), 0);
        ct.ThrowIfCancellationRequested();
        RestoreInterior(blurred, source, padding);
        return blurred.Clone();
    }

    public static Mat ApplyToChannel(Mat bgr, int channelIndex, Func<Mat, Mat> operation)
    {
        using var split = ChannelSplit.Of(bgr);
        using var adjusted = operation(split[channelIndex]);
        adjusted.CopyTo(split[channelIndex]);
        var result = new Mat();
        Cv2.Merge(split.Channels, result);
        return result;
    }

    public static Mat ApplyLutPerChannel(Mat bgr, int channelIndex, Mat lut)
    {
        return ApplyToChannel(bgr, channelIndex, ch =>
        {
            var adjusted = new Mat();
            Cv2.LUT(ch, lut, adjusted);
            return adjusted;
        });
    }

    public static Mat BuildLut(Func<int, double> map)
    {
        var lut = new byte[256];
        for (int i = 0; i < lut.Length; i++)
        {
            lut[i] = (byte)Math.Round(Math.Clamp(map(i), 0.0, 255.0));
        }

        var lutMat = new Mat(1, 256, MatType.CV_8UC1);
        lutMat.SetArray(lut);
        return lutMat;
    }

    public static void ApplyLut(this Mat mat, Func<int, double> map)
    {
        using var lutMat = BuildLut(map);
        Cv2.LUT(mat, lutMat, mat);
    }

    public static Mat AdjustChannel(Mat bgr, int channelIndex, double gain, double offset)
    {
        using var split = ChannelSplit.Of(bgr);
        using var adjusted = new Mat();
        split[channelIndex].ConvertTo(adjusted, MatType.CV_8UC1, gain, offset);
        adjusted.CopyTo(split[channelIndex]);
        var result = new Mat();
        Cv2.Merge(split.Channels, result);
        return result;
    }

    public static Mat ColorBalance(Mat bgr, double temperature, double tint)
    {
        if (Math.Abs(temperature) < Epsilon && Math.Abs(tint) < Epsilon)
        {
            return bgr.Clone();
        }

        using var split = ChannelSplit.Of(bgr);
        try
        {
            if (Math.Abs(temperature) > Epsilon)
            {
                double tempShift = temperature * 0.5;
                Cv2.Add(split[0], Scalar.All(-tempShift), split[0]);
                Cv2.Add(split[2], Scalar.All(tempShift), split[2]);
            }

            if (Math.Abs(tint) > Epsilon)
            {
                double tintShift = tint * 0.5;
                Cv2.Add(split[1], Scalar.All(-tintShift), split[1]);
            }

            var result = new Mat();
            Cv2.Merge(split.Channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in split.Channels) ch.Dispose();
        }
    }

    public static Mat ApplySepia(Mat input)
    {
        using var sepia = new Mat(3, 3, MatType.CV_32FC1);
        sepia.SetArray(new[]
        {
            0.131f, 0.534f, 0.272f,
            0.168f, 0.686f, 0.349f,
            0.189f, 0.769f, 0.393f
        });
        var result = new Mat();
        Cv2.Transform(input, result, sepia);
        return result;
    }
}
