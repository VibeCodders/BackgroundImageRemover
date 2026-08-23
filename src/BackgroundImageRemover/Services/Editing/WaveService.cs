using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Wavy (ripple) distortion of the image via a sinusoidal remap.</summary>
public static class WaveService
{
    /// <summary>
    /// Displaces pixels with a sine wave: ridges run perpendicular to <paramref name="angleDeg"/>
    /// (0° = horizontal ridges), <paramref name="wavelength"/> sets their spacing and
    /// <paramref name="amplitude"/> the displacement (0 disables the effect). The caller owns the
    /// returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, double amplitude, double wavelength, double angleDeg)
    {
        if (amplitude < 0.5 || bgr.Width <= 1 || bgr.Height <= 1)
        {
            return bgr.Clone();
        }

        int w = bgr.Width;
        int h = bgr.Height;
        double wl = Math.Max(1.0, wavelength);
        double rad = angleDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        using var mapX = new Mat(h, w, MatType.CV_32FC1);
        using var mapY = new Mat(h, w, MatType.CV_32FC1);
        PixelLoop.ForEach(h, w, (y, x) =>
        {
            // Coordinate along the wave direction.
            double u = x * cosA + y * sinA;
            double offset = amplitude * Math.Sin(2.0 * Math.PI * u / wl);
            mapX.Set<float>(y, x, (float)(x - offset * sinA));
            mapY.Set<float>(y, x, (float)(y + offset * cosA));
        });

        var result = new Mat();
        Cv2.Remap(bgr, result, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Replicate);
        return result;
    }
}
