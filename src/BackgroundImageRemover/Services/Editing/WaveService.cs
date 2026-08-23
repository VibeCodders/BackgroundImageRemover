using System.Threading.Tasks;
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
        // Each map entry is an independent per-pixel computation, so rows are built in parallel.
        unsafe
        {
            byte* xPtr = (byte*)mapX.DataPointer;
            byte* yPtr = (byte*)mapY.DataPointer;
            long xStep = mapX.Step();
            long yStep = mapY.Step();
            Parallel.For(0, h, y =>
            {
                var mapXRow = new Span<float>((float*)(xPtr + y * xStep), w);
                var mapYRow = new Span<float>((float*)(yPtr + y * yStep), w);
                for (int x = 0; x < w; x++)
                {
                    // Coordinate along the wave direction.
                    double u = x * cosA + y * sinA;
                    double offset = amplitude * Math.Sin(2.0 * Math.PI * u / wl);
                    mapXRow[x] = (float)(x - offset * sinA);
                    mapYRow[x] = (float)(y + offset * cosA);
                }
            });
        }

        var result = new Mat();
        Cv2.Remap(bgr, result, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Replicate);
        return result;
    }
}
