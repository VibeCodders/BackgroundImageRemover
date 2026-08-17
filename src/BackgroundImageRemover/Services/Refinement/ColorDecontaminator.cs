using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Removes the original background color's cast ("spill") from the semi-transparent edge
/// pixels of a cutout. Strategy masks are feathered, so edge pixels keep the source image's
/// RGB even where the alpha is partial; without this step the old background shows up as a
/// colored halo once the cutout is composited over a new background.
/// </summary>
public static class ColorDecontaminator
{
    private const int LocalEstimateKernel = 31; // neighborhood over which the background color is estimated
    private const float DensityThreshold = 1e-4f;

    /// <summary>
    /// Decontaminates <paramref name="bgra"/> in place, recovering the pure foreground color
    /// as F = (C - (1-a)*B) / a for every 0 &lt; a &lt; 1 pixel, where C is the observed color,
    /// a the alpha and B the background color. When <paramref name="knownBackground"/> is null,
    /// B is estimated per pixel from the surrounding fully-transparent pixels.
    /// </summary>
    public static void Decontaminate(Mat bgra, Vec3b? knownBackground)
    {
        if (bgra.Channels() != 4)
        {
            return;
        }

        var channels = Cv2.Split(bgra);
        try
        {
            var (bgB, bgG, bgR, density) = EstimateBackground(channels, knownBackground);

            using var alphaF = new Mat();
            channels[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

            channels[0].GetArray(out byte[] b);
            channels[1].GetArray(out byte[] g);
            channels[2].GetArray(out byte[] r);
            alphaF.GetArray(out float[] alpha);
            bgB.GetArray(out float[] bb);
            bgG.GetArray(out float[] bg2);
            bgR.GetArray(out float[] br);

            float[]? densityPixels = null;
            if (density is not null)
            {
                density.GetArray(out float[] d);
                densityPixels = d;
            }

            for (int i = 0; i < b.Length; i++)
            {
                float a = alpha[i];
                if (a <= 0f || a >= 1f)
                {
                    continue;
                }
                // Without a reliable local background estimate the pixel is left untouched.
                if (densityPixels is not null && densityPixels[i] < DensityThreshold)
                {
                    continue;
                }

                float inv = 1f / a;
                float w = 1f - a;
                b[i] = ClampToByte((b[i] - w * bb[i]) * inv);
                g[i] = ClampToByte((g[i] - w * bg2[i]) * inv);
                r[i] = ClampToByte((r[i] - w * br[i]) * inv);
            }

            channels[0].SetArray(b);
            channels[1].SetArray(g);
            channels[2].SetArray(r);

            Cv2.Merge(channels, bgra);
        }
        finally
        {
            foreach (var c in channels)
            {
                c.Dispose();
            }
        }
    }

    /// <summary>Estimates the background color at every pixel, returning BGR float Mats plus the estimation density (null for a known color).</summary>
    private static (Mat B, Mat G, Mat R, Mat? Density) EstimateBackground(Mat[] channels, Vec3b? knownBackground)
    {
        if (knownBackground is { } kb)
        {
            return (
                ConstantFloat(channels[0].Size(), kb.Item0),
                ConstantFloat(channels[0].Size(), kb.Item1),
                ConstantFloat(channels[0].Size(), kb.Item2),
                null);
        }

        // Fully transparent (alpha == 0) pixels are known background; everything else is excluded.
        using var bgMask = new Mat();
        Cv2.Threshold(channels[3], bgMask, 0, 255, ThresholdTypes.BinaryInv);
        using var maskF = new Mat();
        bgMask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);

        var kernel = new Size(LocalEstimateKernel, LocalEstimateKernel);

        // Normalized box filter of the mask = local fraction of background pixels (0..1).
        using var den = new Mat();
        Cv2.BoxFilter(maskF, den, MatType.CV_32F, kernel);
        var density = den.Clone();

        // Local mean background color per channel = box(channel*mask) / box(mask).
        // Where the density is ~0 the division yields NaN/Inf, but those pixels are skipped later.
        var result = new Mat[3];
        for (int c = 0; c < 3; c++)
        {
            using var channelF = new Mat();
            channels[c].ConvertTo(channelF, MatType.CV_32FC1);
            using var weighted = channelF.Mul(maskF).ToMat();
            using var num = new Mat();
            Cv2.BoxFilter(weighted, num, MatType.CV_32F, kernel);
            using var est = new Mat();
            Cv2.Divide(num, den, est);
            result[c] = est.Clone();
        }

        return (result[0], result[1], result[2], density);
    }

    private static Mat ConstantFloat(Size size, float value)
        => new(size, MatType.CV_32FC1, new Scalar(value));

    private static byte ClampToByte(float value)
        => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
}
