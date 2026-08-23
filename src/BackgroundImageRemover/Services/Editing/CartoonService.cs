using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Cartoon look: smoothed flat colors with dark outlines.</summary>
public static class CartoonService
{
    /// <summary>
    /// Applies a cartoon effect to <paramref name="bgr"/>: bilateral smoothing removes texture,
    /// colors are quantized to <paramref name="quantizeLevels"/> steps per channel, and a
    /// thresholded edge mask darkens the outlines. <paramref name="smoothness"/> is the bilateral
    /// filter radius (1..20), <paramref name="edgeThreshold"/> the adaptive-threshold block radius
    /// (0 disables the outline pass). The caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, int smoothness, int quantizeLevels, int edgeThreshold)
    {
        // 1. Smooth out texture details.
        using var smooth = new Mat();
        int d = Math.Clamp(smoothness, 1, 20) * 2 + 1;
        Cv2.BilateralFilter(bgr, smooth, d, 60, 60);

        // 2. Quantize each channel to a small number of levels (flat cartoon colors).
        int levels = Math.Clamp(quantizeLevels, 2, 32);
        double step = 256.0 / levels;
        using var lut = new Mat(1, 256, MatType.CV_8UC1);
        for (int i = 0; i < 256; i++)
        {
            int v = (int)Math.Round(i / step) * (int)step;
            lut.Set<byte>(0, i, (byte)Math.Min(255, v));
        }
        using var quantized = new Mat();
        Cv2.LUT(smooth, lut, quantized);

        // 3. Optional dark outlines from an adaptive threshold on the luminance.
        if (edgeThreshold > 0)
        {
            using var gray = new Mat();
            Cv2.CvtColor(quantized, gray, ColorConversionCodes.BGR2GRAY);
            int block = Math.Clamp(edgeThreshold, 1, 40) * 2 + 1;
            using var edges = new Mat();
            Cv2.AdaptiveThreshold(gray, edges, 255, AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.Binary, block, 2);
            using var edgeMask = new Mat();
            Cv2.BitwiseNot(edges, edgeMask);
            var result = quantized.Clone();
            using var dark = new Mat(result.Size(), MatType.CV_8UC3, Scalar.All(0));
            dark.CopyTo(result, edgeMask);
            return result;
        }

        return quantized.Clone();
    }
}
