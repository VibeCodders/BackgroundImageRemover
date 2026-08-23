using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Glow / bloom: bright areas radiate a soft halo of light.</summary>
public static class GlowService
{
    /// <summary>
    /// Adds a glow to <paramref name="bgr"/>: pixels brighter than <paramref name="threshold"/>
    /// (0..255) become the glow source, are blurred by <paramref name="radius"/> and added back
    /// with <paramref name="strength"/> (0..2). The caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, int threshold, int radius, double strength)
    {
        strength = Math.Clamp(strength, 0, 2);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, Math.Clamp(threshold, 0, 255), 255, ThresholdTypes.Binary);

        if (Cv2.CountNonZero(mask) == 0)
        {
            // Nothing is bright enough to glow.
            return bgr.Clone();
        }

        using var glowSource = new Mat();
        Cv2.BitwiseAnd(bgr, bgr, glowSource, mask);

        using var glow = new Mat();
        int k = Math.Max(1, radius) * 2 + 1;
        Cv2.GaussianBlur(glowSource, glow, new OpenCvSharp.Size(k, k), 0);

        var result = new Mat();
        Cv2.AddWeighted(bgr, 1.0, glow, strength, 0, result);
        return result;
    }
}
