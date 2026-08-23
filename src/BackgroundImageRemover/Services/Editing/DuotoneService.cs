using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Maps image luminance onto a two-color (duotone) palette. Dark pixels are tinted toward one
/// color and light pixels toward another. Used by the Duotone tool.
/// </summary>
public static class DuotoneService
{
    /// <summary>
    /// Applies a duotone effect to <paramref name="bgr"/>. <paramref name="midpoint"/> (0..1)
    /// selects the luminance threshold around which colors transition; <paramref name="strength"/>
    /// (0..1) blends the effect back toward the original image (0 = no change, 1 = full duotone).
    /// Lower <paramref name="strength"/> also makes the transition less crisp.
    /// </summary>
    public static Mat Apply(Mat bgr, Vec3b darkColor, Vec3b lightColor, double midpoint, double strength)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        midpoint = Math.Clamp(midpoint, 0.0, 1.0);
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= EditingGuard.Epsilon)
        {
            return bgr.Clone();
        }

        int w = bgr.Width;
        int h = bgr.Height;

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        // Transition width shrinks as strength grows (strength 1 => near-hard threshold).
        double transition = 0.5 * (1.0 - strength);
        if (transition < 1e-3)
        {
            transition = 1e-3;
        }

        var result = new Mat(h, w, MatType.CV_8UC3);
        try
        {
            var dark = new Vec3b(darkColor[0], darkColor[1], darkColor[2]);
            var light = new Vec3b(lightColor[0], lightColor[1], lightColor[2]);

            // Bulk array access: one copy in/out instead of native interop per pixel.
            byte[] grayData = PixelLoop.GetData<byte>(gray);
            Vec3b[] srcData = PixelLoop.GetData<Vec3b>(bgr);
            var dstData = new Vec3b[srcData.Length];
            for (int i = 0; i < srcData.Length; i++)
            {
                double lum = grayData[i] / 255.0;
                double z = (lum - midpoint) / transition;
                double f = SmoothStep(-0.5, 0.5, z);

                var target = PixelColor.Blend(dark, light, f);
                dstData[i] = PixelColor.Blend(srcData[i], target, strength);
            }
            PixelLoop.SetData(result, dstData);

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}
