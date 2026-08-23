using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Emboss (relief) filter with an adjustable light direction and strength.</summary>
public static class EmbossService
{
    // 3x3 emboss kernels for light coming from each of the 8 compass directions. Every kernel
    // sums to zero so the overall luminance is preserved; a +128 offset centers the result.
    private static readonly float[][,] Kernels =
    [
        new float[,] { { -1, 0, 1 }, { -1, 0, 1 }, { -1, 0, 1 } },   // 0°   light from left
        new float[,] { { 0, 1, 1 }, { -1, 0, 1 }, { -1, -1, 0 } },   // 45°
        new float[,] { { 1, 1, 1 }, { 0, 0, 0 }, { -1, -1, -1 } },   // 90°  light from top
        new float[,] { { 1, 1, 0 }, { 1, 0, -1 }, { 0, -1, -1 } },   // 135°
        new float[,] { { 1, 0, -1 }, { 1, 0, -1 }, { 1, 0, -1 } },   // 180° light from right
        new float[,] { { 0, -1, -1 }, { 1, 0, -1 }, { 1, 1, 0 } },   // 225°
        new float[,] { { -1, -1, -1 }, { 0, 0, 0 }, { 1, 1, 1 } },   // 270° light from bottom
        new float[,] { { -1, -1, 0 }, { -1, 0, 1 }, { 0, 1, 1 } }    // 315°
    ];

    /// <summary>
    /// Applies an emboss relief to <paramref name="bgr"/>. <paramref name="angleDeg"/> snaps to
    /// the nearest 45° compass direction; <paramref name="strength"/> scales the kernel. When
    /// <paramref name="grayscale"/> is true the relief is computed on the luminance and the color
    /// is dropped (classic emboss); otherwise each channel is embossed independently. The caller
    /// owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, double angleDeg, double strength, bool grayscale)
    {
        int idx = ((int)Math.Round(((angleDeg % 360) + 360) % 360 / 45.0)) % 8;
        var kernelData = Kernels[idx];
        float s = Math.Max(0.05f, (float)strength);

        using var kernel = new Mat(3, 3, MatType.CV_32F);
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                kernel.Set<float>(r, c, kernelData[r, c] * s);
            }
        }

        var result = new Mat();
        if (grayscale)
        {
            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            using var relief = new Mat();
            Cv2.Filter2D(gray, relief, MatType.CV_8U, kernel, anchor: new Point(-1, -1), delta: 128);
            Cv2.CvtColor(relief, result, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            Cv2.Filter2D(bgr, result, MatType.CV_8U, kernel, anchor: new Point(-1, -1), delta: 128);
        }

        return result;
    }
}
