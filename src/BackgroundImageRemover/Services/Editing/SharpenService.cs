using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Selective and whole-image sharpening operations for the Sharpen tool.</summary>
public static class SharpenService
{
    /// <summary>
    /// Sharpens a painted (non-zero) region of <paramref name="mask"/> with an unsharp-mask
    /// of <paramref name="strength"/> (0..1), leaving the rest of the image untouched.
    /// </summary>
    public static Mat SharpenRegion(Mat bgr, Mat mask, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        if (!EditingGuard.IsEffectSignificant(strength))
        {
            return bgr.Clone();
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(0, 0), 2, 2);
        using var sharpened = new Mat();
        // unsharp mask: result = original + strength * (original - blurred)
        Cv2.AddWeighted(bgr, 1.0 + strength, blurred, -strength, 0, sharpened);

        return bgr.BlendByMask(sharpened, mask);
    }

    /// <summary>Sharpens the entire image with an unsharp mask of <paramref name="strength"/> (0..1).</summary>
    public static Mat SharpenAll(Mat bgr, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        if (!EditingGuard.IsEffectSignificant(strength))
        {
            return bgr.Clone();
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(0, 0), 2, 2);
        var result = new Mat();
        Cv2.AddWeighted(bgr, 1.0 + strength, blurred, -strength, 0, result);
        return result;
    }
}
