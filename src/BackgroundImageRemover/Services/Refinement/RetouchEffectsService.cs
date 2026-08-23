using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Whole-image retouch effects that operate on the pixel content behind a cutout's alpha
/// channel. Each method returns a new BGR Mat (the caller keeps the alpha unchanged).
/// </summary>
public static class RetouchEffectsService
{
    /// <summary>Dehazes a hazy image via local contrast equalization, blended by <paramref name="strength"/> (0..1).</summary>
    public static Mat Dehaze(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        using var enhanced = ImageProcessingUtility.ApplyClahe(bgr);
        {
            var result = new Mat();
            Cv2.AddWeighted(bgr, 1.0 - strength, enhanced, strength, 0, result);
            return result;
        }
    }

    /// <summary>Removes the background color cast from semi-transparent edge pixels (defringe).</summary>
    public static Mat Defringe(Mat bgr, Mat alpha)
    {
        using var bgra = bgr.ToBgra(alpha);
        BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);
        ColorDecontaminator.Decontaminate(bgra, knownBackground: null);
        using var split = ChannelSplit.Of(bgra);
        var result = new Mat();
        Cv2.Merge(new[] { split[0], split[1], split[2] }, result);
        return result;
    }

    /// <summary>Blurs the areas outside the foreground (low alpha), leaving the subject sharp.</summary>
    public static Mat BlurBackground(Mat bgr, Mat alpha, double radius)
    {
        radius = Math.Max(0, radius);
        if (radius <= 1e-4)
        {
            return bgr.Clone();
        }

        int kernel = Math.Max(1, (int)Math.Round(radius * 2) | 1);
        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(kernel, kernel), radius, radius);
        return blurred.BlendByMask(bgr, alpha); // keep original where alpha is high, blurred where low
    }

    /// <summary>Sharpens only the foreground pixels (high alpha) via unsharp masking.</summary>
    public static Mat SharpenSubject(Mat bgr, Mat alpha, double strength)
    {
        strength = Math.Max(0, strength);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(0, 0), 3);
        using var sharpened = new Mat();
        Cv2.AddWeighted(bgr, 1.0 + strength, blurred, -strength, 0, sharpened);
        return bgr.BlendByMask(sharpened, alpha); // keep background untouched, sharpen the subject
    }

    /// <summary>Removes dust specks via a median filter.</summary>
    public static Mat RemoveDust(Mat bgr, int kernelSize)
    {
        int k = EditingGuard.EnsureOdd(kernelSize);
        var result = new Mat();
        Cv2.MedianBlur(bgr, result, k);
        return result;
    }

    /// <summary>Edge-preserving surface smoothing (bilateral), scaled by strength (0..1).</summary>
    public static Mat SurfaceBlur(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        var result = new Mat();
        Cv2.BilateralFilter(bgr, result, 5, strength * 120.0, strength * 60.0);
        return result;
    }

    /// <summary>One-click automatic contrast via CLAHE on the Luminance channel.</summary>
    public static Mat AutoContrast(Mat bgr) => ImageProcessingUtility.ApplyClahe(bgr);

    /// <summary>One-click gray-world automatic white balance.</summary>
    public static Mat AutoWhiteBalance(Mat bgr) => ImageProcessingUtility.AutoWhiteBalance(bgr);

    /// <summary>Radial chromatic aberration (delegates to the shared FX implementation).</summary>
    public static Mat ChromaticAberration(Mat bgr, double strength) => FxService.ChromaticAberration(bgr, strength);

    /// <summary>Boosts saturation only in the foreground pixels (high alpha).</summary>
    public static Mat ColorBoost(Mat bgr, Mat alpha, double amount)
    {
        if (Math.Abs(amount) <= 1e-4)
        {
            return bgr.Clone();
        }

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        using var boosted = new Mat();
        using (var split = ChannelSplit.Of(hsv))
        {
            split[1].ConvertTo(split[1], MatType.CV_8UC1, 1.0 + amount);
            Cv2.Merge(split.Channels, hsv);
            Cv2.CvtColor(hsv, boosted, ColorConversionCodes.HSV2BGR);
        }

        return bgr.BlendByMask(boosted, alpha);
    }
}
