using BackgroundImageRemover.Services.Compositing;
using CommunityToolkit.HighPerformance;
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
    /// scaled by <paramref name="opacity"/> (0..1). Single-pass over zero-copy
    /// <see cref="Span2D{T}"/> views: the previous version materialized ~11 intermediate
    /// CV_32F Mats. Blend math is identical (<c>result = base*(1-a) + overlay*a</c> with
    /// <c>a = overlayAlpha*opacity/255</c>) and the result alpha stays the base alpha.
    /// </summary>
    public static Mat CompositeOverBgra(Mat baseBgra, Mat overlayBgra, double opacity)
    {
        var baseSpan = baseBgra.AsSpan2D<Vec4b>();
        var ovSpan = overlayBgra.AsSpan2D<Vec4b>();
        float op = (float)opacity;

        var result = new Mat(baseBgra.Size(), MatType.CV_8UC4);
        var dstSpan = result.AsSpan2D<Vec4b>();
        for (int y = 0; y < dstSpan.Height; y++)
        {
            for (int x = 0; x < dstSpan.Width; x++)
            {
                var b = baseSpan[y, x];
                var o = ovSpan[y, x];
                float a = o.Item3 * op / 255f;
                float inv = 1f - a;
                dstSpan[y, x] = new Vec4b(
                    BlendByte(o.Item0, b.Item0, inv, a),
                    BlendByte(o.Item1, b.Item1, inv, a),
                    BlendByte(o.Item2, b.Item2, inv, a),
                    b.Item3);
            }
        }
        return result;
    }

    /// <summary>
    /// Alpha-composites <paramref name="overlayRoi"/> onto <paramref name="dstRoi"/> in place,
    /// scaled by <paramref name="opacity"/> (0..1). Single-pass over zero-copy
    /// <see cref="Span2D{T}"/> views; the previous version materialized ~8 intermediate
    /// CV_32F Mats per call (text overlay and shape/mosaic stamping call it per block).
    /// </summary>
    public static Mat AlphaComposite(Mat dstRoi, Mat overlayRoi, double opacity)
    {
        var dstSpan = dstRoi.AsSpan2D<Vec3b>();
        var ovSpan = overlayRoi.AsSpan2D<Vec4b>();
        float op = (float)opacity;
        for (int y = 0; y < dstSpan.Height; y++)
        {
            for (int x = 0; x < dstSpan.Width; x++)
            {
                var d = dstSpan[y, x];
                var o = ovSpan[y, x];
                float a = o.Item3 * op / 255f;
                float inv = 1f - a;
                dstSpan[y, x] = new Vec3b(
                    BlendByte(o.Item0, d.Item0, inv, a),
                    BlendByte(o.Item1, d.Item1, inv, a),
                    BlendByte(o.Item2, d.Item2, inv, a));
            }
        }
        return dstRoi;
    }

    private static byte BlendByte(byte fg, byte bg, float inv, float a)
    {
        float v = fg * a + bg * inv;
        return (byte)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
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
