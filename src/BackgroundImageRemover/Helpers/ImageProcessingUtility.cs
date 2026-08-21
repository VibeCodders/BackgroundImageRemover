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

    public static Mat CompositeOverBgra(Mat baseBgra, Mat overlayBgra, double opacity)
    {
        using var bsplit = ChannelSplit.Of(baseBgra);
        using var osplit = ChannelSplit.Of(overlayBgra);
        using var a = new Mat();
        osplit[3].ConvertTo(a, MatType.CV_32FC1, opacity / 255.0);

        var channels = new Mat[4];
        try
        {
            for (int i = 0; i < 3; i++)
            {
                using var baseF = new Mat();
                bsplit[i].ConvertTo(baseF, MatType.CV_32FC1);
                using var overF = new Mat();
                osplit[i].ConvertTo(overF, MatType.CV_32FC1);
                using var inv = new Mat();
                Cv2.Subtract(new Mat(a.Size(), a.Type(), Scalar.All(1.0)), a, inv);
                using var baseWeighted = baseF.Mul(inv).ToMat();
                using var overWeighted = overF.Mul(a).ToMat();
                channels[i] = (baseWeighted + overWeighted).ToMat();
            }

            channels[3] = new Mat();
            bsplit[3].ConvertTo(channels[3], MatType.CV_32FC1);
            var merged = new Mat();
            Cv2.Merge(channels, merged);
            using (merged)
            {
                var result = new Mat();
                merged.ConvertTo(result, MatType.CV_8UC4);
                return result;
            }
        }
        finally
        {
            foreach (var ch in channels) ch?.Dispose();
        }
    }

    public static Mat AlphaComposite(Mat dstRoi, Mat overlayRoi, double opacity)
    {
        using var channels = ChannelSplit.Of(overlayRoi);
        using var alpha = new Mat();
        channels[3].ConvertTo(alpha, MatType.CV_32FC1, opacity / 255.0);

        using var overlayBgr = new Mat();
        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, overlayBgr);

        using var overlayF = new Mat();
        overlayBgr.ConvertTo(overlayF, MatType.CV_32FC3);
        using var baseF = new Mat();
        dstRoi.ConvertTo(baseF, MatType.CV_32FC3);

        using var alpha3 = new Mat();
        Cv2.CvtColor(alpha, alpha3, ColorConversionCodes.GRAY2BGR);

        using var fgWeighted = overlayF.Mul(alpha3).ToMat();
        using var oneMinus = new Mat();
        Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, oneMinus);
        using var bgWeighted = baseF.Mul(oneMinus).ToMat();

        using var blended = (fgWeighted + bgWeighted).ToMat();
        blended.ConvertTo(dstRoi, MatType.CV_8UC3);
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

    public static Mat AdjustSaturation(Mat bgr, double boost)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        using var split = ChannelSplit.Of(hsv);
        split[1].ConvertTo(split[1], MatType.CV_8UC1, 1.0 + boost);
        var result = new Mat();
        Cv2.Merge(split.Channels, result);
        using (result)
        {
            var output = new Mat();
            Cv2.CvtColor(result, output, ColorConversionCodes.HSV2BGR);
            return output;
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
}
