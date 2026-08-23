using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Thermal (heatmap) palette: maps luminance onto a blue→cyan→green→yellow→red ramp.</summary>
public static class ThermalService
{
    /// <summary>
    /// Applies a thermal/heatmap palette to <paramref name="bgr"/>. Each pixel's luminance is
    /// mapped onto a blue→cyan→green→yellow→red ramp; <paramref name="intensity"/> (0..2) blends
    /// between the original grayscale (0) and the full palette (1, with values above 1 oversaturating).
    /// <paramref name="invert"/> flips the ramp (bright pixels become cold). The caller owns the
    /// returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, double intensity, bool invert)
    {
        intensity = Math.Clamp(intensity, 0, 2);

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        if (invert)
        {
            Cv2.BitwiseNot(gray, gray);
        }

        // Build one 256-entry LUT per channel that already folds in the intensity blend.
        using var lutB = ImageProcessingUtility.BuildLut(i => i + (ThermalB(i / 255.0) - i) * intensity);
        using var lutG = ImageProcessingUtility.BuildLut(i => i + (ThermalG(i / 255.0) - i) * intensity);
        using var lutR = ImageProcessingUtility.BuildLut(i => i + (ThermalR(i / 255.0) - i) * intensity);

        using var chB = new Mat();
        using var chG = new Mat();
        using var chR = new Mat();
        Cv2.LUT(gray, lutB, chB);
        Cv2.LUT(gray, lutG, chG);
        Cv2.LUT(gray, lutR, chR);

        var result = new Mat(bgr.Size(), MatType.CV_8UC3);
        Cv2.Merge(new[] { chB, chG, chR }, result);
        return result;
    }

    // Piecewise-linear ramp stops (BGR) of the classic thermal palette.
    private static readonly (double T, double B, double G, double R)[] Stops =
    {
        (0.00, 160, 40, 20),   // deep blue
        (0.25, 220, 160, 30),  // cyan
        (0.50, 180, 220, 70),  // green-yellow
        (0.75, 60, 200, 210),  // orange
        (1.00, 30, 50, 250),   // red-white
    };

    private static double Interp(double t, Func<(double T, double B, double G, double R), double> pick)
    {
        for (int s = 0; s < Stops.Length - 1; s++)
        {
            var a = Stops[s];
            var b = Stops[s + 1];
            if (t <= b.T)
            {
                double k = b.T - a.T <= 1e-9 ? 0 : (t - a.T) / (b.T - a.T);
                return a.T == b.T ? pick(a) : pick(a) + (pick(b) - pick(a)) * k;
            }
        }

        return pick(Stops[^1]);
    }

    private static double ThermalB(double t) => Interp(t, s => s.B);
    private static double ThermalG(double t) => Interp(t, s => s.G);
    private static double ThermalR(double t) => Interp(t, s => s.R);
}
