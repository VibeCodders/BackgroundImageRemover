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

        double wl = Math.Max(1.0, wavelength);
        double rad = angleDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        // Each map entry is an independent per-pixel computation; RemapHelper fills the maps in
        // parallel and applies Cv2.Remap (replicate border keeps the edges stretched).
        return RemapHelper.Remap(bgr, (x, y, mapXRow, mapYRow) =>
        {
            // Coordinate along the wave direction.
            double u = x * cosA + y * sinA;
            double offset = amplitude * Math.Sin(2.0 * Math.PI * u / wl);
            mapXRow[x] = (float)(x - offset * sinA);
            mapYRow[x] = (float)(y + offset * cosA);
        });
    }
}
