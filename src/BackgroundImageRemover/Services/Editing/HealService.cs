using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Repair operations for the Heal tool: inpaint a painted mask and remove dust, scratches
/// or sensor noise, plus edge-preserving smoothing and detail enhancement.
/// </summary>
public static class HealService
{
    /// <summary>Inpaints the painted (non-zero) regions of <paramref name="mask"/> using the surrounding pixels.</summary>
    public static Mat HealRegion(Mat bgr, Mat mask, double radius, InpaintTypes method)
    {
        radius = Math.Max(0.1, radius);
        var result = new Mat();
        Cv2.Inpaint(bgr, mask, result, radius, method);
        return result;
    }

    /// <summary>Removes dust specks via a median filter (larger kernel removes bigger specks).</summary>
    public static Mat RemoveDust(Mat bgr, int kernelSize)
    {
        int k = EditingGuard.EnsureOdd(kernelSize);
        var result = new Mat();
        Cv2.MedianBlur(bgr, result, k);
        return result;
    }

    /// <summary>Removes scratches/noise with non-local means denoising, scaled by <paramref name="strength"/> (0..1).</summary>
    public static Mat RemoveScratches(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        float h = (float)(3.0 + strength * 10.0);
        var result = new Mat();
        Cv2.FastNlMeansDenoisingColored(bgr, result, h, h, 7, 21);
        return result;
    }

    /// <summary>Edge-preserving surface smoothing (bilateral filter), scaled by <paramref name="strength"/> (0..1).</summary>
    public static Mat SurfaceSmooth(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        var result = new Mat();
        Cv2.BilateralFilter(bgr, result, 5, strength * 120.0, strength * 60.0);
        return result;
    }

    /// <summary>Local detail enhancement, scaled by <paramref name="strength"/> (0..1).</summary>
    public static Mat DetailEnhance(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        var result = new Mat();
        Cv2.DetailEnhance(bgr, result, 10f, (float)(0.15 + strength * 0.6));
        return result;
    }
}
