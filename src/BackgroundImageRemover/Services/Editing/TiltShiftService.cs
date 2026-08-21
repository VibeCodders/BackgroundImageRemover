using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Tilt-shift / miniature effect: a sharp focus band with progressively blurred surroundings.</summary>
public static class TiltShiftService
{
    /// <summary>
    /// Blurs everything outside a focus band (positioned by <paramref name="focusCenter"/> 0..1 with
    /// <paramref name="focusWidth"/> 0..1), then boosts saturation for the miniature look.
    /// When <paramref name="vertical"/> is true the band runs top-to-bottom instead of left-to-right.
    /// </summary>
    public static Mat Apply(
        Mat bgr,
        double focusCenter,
        double focusWidth,
        double blurRadius,
        bool vertical,
        double saturationBoost)
    {
        focusCenter = Math.Clamp(focusCenter, 0.0, 1.0);
        focusWidth = Math.Clamp(focusWidth, 0.0, 1.0);
        blurRadius = Math.Max(0, blurRadius);

        var current = bgr.Clone();
        try
        {
            if (blurRadius > 1e-4)
            {
                using var blurred = new Mat();
                int kernel = ImageProcessingUtility.GaussianKernelSize(blurRadius);
                Cv2.GaussianBlur(current, blurred, new Size(kernel, kernel), blurRadius, blurRadius);
                using var mask = BuildFocusMask(current.Size(), focusCenter, focusWidth, vertical);
                // Blend blurred image in where the mask is white, keep original where black.
                var blended = current.BlendByMask(blurred, mask);
                current.Dispose();
                current = blended;
            }

            if (Math.Abs(saturationBoost) > 1e-4)
            {
                var boosted = ImageProcessingUtility.AdjustSaturation(current, saturationBoost);
                current.Dispose();
                current = boosted;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    /// <summary>Builds a 0 (in focus) to 255 (fully blurred) mask with a soft ramp outside the band.</summary>
    private static Mat BuildFocusMask(Size size, double focusCenter, double focusWidth, bool vertical)
    {
        var mask = new Mat(size, MatType.CV_8UC1, Scalar.All(0));
        int length = vertical ? size.Width : size.Height;
        int bandCenter = (int)Math.Round(focusCenter * length);
        int bandHalf = (int)Math.Round(focusWidth * length / 2.0);

        for (int i = 0; i < length; i++)
        {
            int d = Math.Max(0, Math.Abs(i - bandCenter) - bandHalf);
            double t = Math.Min(1.0, d / (double)Math.Max(1, length - bandHalf));
            byte v = (byte)Math.Round(255.0 * t);
            if (vertical)
            {
                Cv2.Rectangle(mask, new Rect(i, 0, 1, size.Height), Scalar.All(v), -1);
            }
            else
            {
                Cv2.Rectangle(mask, new Rect(0, i, size.Width, 1), Scalar.All(v), -1);
            }
        }

        return mask;
    }
}
