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
        float[] mapXData = PixelLoop.GetData<float>(mapX);
        float[] mapYData = PixelLoop.GetData<float>(mapY);
        for (int i = 0; i < mapXData.Length; i++)
        {
            int x = i % w;
            int y = i / w;
            // Coordinate along the wave direction.
            double u = x * cosA + y * sinA;
            double offset = amplitude * Math.Sin(2.0 * Math.PI * u / wl);
            mapXData[i] = (float)(x - offset * sinA);
            mapYData[i] = (float)(y + offset * cosA);
        }
        PixelLoop.SetData(mapX, mapXData);
        PixelLoop.SetData(mapY, mapYData);

        var result = new Mat();
        Cv2.Remap(bgr, result, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Replicate);
        return result;
    }
}
