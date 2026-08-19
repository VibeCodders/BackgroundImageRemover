using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Vignette effect: darkens or lightens the image edges to draw attention to the center.</summary>
public static class VignetteService
{
    /// <summary>
    /// Applies a vignette to <paramref name="bgr"/>. <paramref name="strength"/> is 0..1 (0 = no
    /// effect). <paramref name="roundness"/> controls the shape of the vignette (0 = oval,
    /// 1 = perfectly circular). <paramref name="feather"/> controls the softness of the edge.
    /// When <paramref name="invert"/> is true the vignette lightens the edges instead of darkening them.
    /// </summary>
    public static Mat Apply(Mat bgr, double strength, double roundness = 0.5, double feather = 0.5, bool invert = false)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        int w = bgr.Width;
        int h = bgr.Height;
        double cx = w / 2.0;
        double cy = h / 2.0;
        double maxRadius = Math.Sqrt(cx * cx + cy * cy);
        double radius = maxRadius * (1.0 - strength * 0.3);

        // Build a radial falloff mask (0 at center, strength at corners).
        var mask = new Mat(h, w, MatType.CV_32FC1, Scalar.All(0));
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double dx = (x - cx) / (w / 2.0);
                double dy = (y - cy) / (h / 2.0);
                // Roundness blends between elliptical (0) and circular (1).
                double r = Math.Sqrt(dx * dx * (1 - roundness) + dy * dy * (1 - roundness) + dx * dx * roundness + dy * dy * roundness);
                double dist = r * maxRadius * 0.5;
                double t = Math.Clamp(dist / radius, 0.0, 1.0);
                // Feather softens the transition.
                t = Math.Pow(t, 1.0 + feather * 2.0);
                mask.Set(y, x, (float)(t * strength));
            }
        }

        try
        {
            using var mask3 = new Mat();
            Cv2.CvtColor(mask, mask3, ColorConversionCodes.GRAY2BGR);

            using var aF = new Mat();
            bgr.ConvertTo(aF, MatType.CV_32FC3);

            // Build the overlay: white (lighten) or 60% dark (darken).
            using var overlay = invert
                ? new Mat(aF.Size(), aF.Type(), new Scalar(255.0, 255.0, 255.0))
                : (Mat)(aF * 0.6);

            using var inv = new Mat();
            Cv2.Subtract(new Mat(mask3.Size(), mask3.Type(), Scalar.All(1.0)), mask3, inv);
            using var aWeighted = aF.Mul(inv).ToMat();
            using var bWeighted = overlay.Mul(mask3).ToMat();
            using var blended = (aWeighted + bWeighted).ToMat();

            var result = new Mat();
            blended.ConvertTo(result, MatType.CV_8UC3);
            return result;
        }
        catch
        {
            mask.Dispose();
            throw;
        }
    }
}
