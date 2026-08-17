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
    /// <summary>Default neighborhood radius (px) over which the background color is estimated.</summary>
    public const int DefaultEstimateRadius = 15; // matches the original fixed 31x31 kernel
    private const float DensityThreshold = 1e-4f;

    /// <summary>
    /// Decontaminates <paramref name="bgra"/> in place. When <paramref name="knownBackground"/> is
    /// supplied (chroma key), the background color is known exactly and the key's alpha is a soft
    /// key rather than true coverage, so a full unspill is unreliable; instead the dominant
    /// background channel is neutralized (classic spill suppression). Otherwise the background
    /// color is estimated per pixel from the surrounding fully-transparent pixels (within
    /// <paramref name="estimateRadius"/> pixels) and the pure foreground color is recovered as
    /// F = (C - (1-a)*B) / a.
    /// </summary>
    public static void Decontaminate(Mat bgra, Vec3b? knownBackground, int estimateRadius = DefaultEstimateRadius)
    {
        if (bgra.Channels() != 4)
        {
            return;
        }

        var channels = Cv2.Split(bgra);
        try
        {
            if (knownBackground is { } kb)
            {
                Despill(channels, kb);
            }
            else
            {
                Unspill(channels, estimateRadius);
            }

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

    /// <summary>
    /// Classic chroma-key spill suppression: on semi-transparent edge pixels, pull the key
    /// color's dominant channel toward the average of the other two, weighted by how much of
    /// the pixel is still background (1 - alpha). This removes the green/blue cast without
    /// assuming the key's alpha equals the true subject coverage.
    /// </summary>
    private static void Despill(Mat[] channels, Vec3b keyColor)
    {
        int dominant = keyColor.Item0 >= keyColor.Item1 && keyColor.Item0 >= keyColor.Item2 ? 0
            : keyColor.Item1 >= keyColor.Item2 ? 1 : 2;

        using var alphaF = new Mat();
        channels[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

        channels[0].GetArray(out byte[] b);
        channels[1].GetArray(out byte[] g);
        channels[2].GetArray(out byte[] r);
        alphaF.GetArray(out float[] alpha);

        var dominantPixels = dominant switch { 0 => b, 1 => g, _ => r };

        for (int i = 0; i < b.Length; i++)
        {
            float a = alpha[i];
            if (a <= 0f || a >= 1f)
            {
                continue;
            }

            double edgeWeight = 1.0 - a;
            byte p0 = b[i], p1 = g[i], p2 = r[i];

            double othersAvg = dominant switch
            {
                0 => (p1 + p2) / 2.0,
                1 => (p0 + p2) / 2.0,
                _ => (p0 + p1) / 2.0
            };

            int d = dominantPixels[i];
            if (d > othersAvg)
            {
                dominantPixels[i] = (byte)Math.Clamp(Math.Round(d - (d - othersAvg) * edgeWeight), 0, 255);
            }
        }

        channels[0].SetArray(b);
        channels[1].SetArray(g);
        channels[2].SetArray(r);
    }

    /// <summary>
    /// Matte-based decontamination: recovers the pure foreground color F = (C - (1-a)*B) / a
    /// for every 0 &lt; a &lt; 1 pixel, with B estimated per pixel from surrounding transparent pixels.
    /// Valid for strategies whose feathered alpha approximates the true subject coverage.
    /// </summary>
    private static void Unspill(Mat[] channels, int estimateRadius)
    {
        var (bgB, bgG, bgR, density) = EstimateBackground(channels, null, estimateRadius);

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
    }

    /// <summary>Estimates the background color at every pixel, returning BGR float Mats plus the estimation density (null for a known color).</summary>
    private static (Mat B, Mat G, Mat R, Mat? Density) EstimateBackground(Mat[] channels, Vec3b? knownBackground, int estimateRadius)
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

        int kernelSize = Math.Max(3, estimateRadius * 2 + 1);
        var kernel = new Size(kernelSize, kernelSize);

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
