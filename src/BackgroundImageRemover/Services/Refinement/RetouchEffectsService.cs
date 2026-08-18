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

        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);
        var labChannels = Cv2.Split(lab);
        Mat enhanced;
        try
        {
            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
            clahe.Apply(labChannels[0], labChannels[0]);
            Cv2.Merge(labChannels, lab);
            enhanced = new Mat();
            Cv2.CvtColor(lab, enhanced, ColorConversionCodes.Lab2BGR);
        }
        finally
        {
            foreach (var ch in labChannels) ch.Dispose();
        }

        using (enhanced)
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
        return BlendByAlpha(blurred, bgr, alpha); // keep original where alpha is high, blurred where low
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
        return BlendByAlpha(bgr, sharpened, alpha); // keep background untouched, sharpen the subject
    }

    /// <summary>Removes dust specks via a median filter.</summary>
    public static Mat RemoveDust(Mat bgr, int kernelSize)
    {
        int k = Math.Max(1, kernelSize) | 1;
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
    public static Mat AutoContrast(Mat bgr)
    {
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);
        var channels = Cv2.Split(lab);
        try
        {
            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
            clahe.Apply(channels[0], channels[0]);
            Cv2.Merge(channels, lab);
            var result = new Mat();
            Cv2.CvtColor(lab, result, ColorConversionCodes.Lab2BGR);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>One-click gray-world automatic white balance.</summary>
    public static Mat AutoWhiteBalance(Mat bgr)
    {
        var means = Cv2.Mean(bgr);
        double avg = (means.Val0 + means.Val1 + means.Val2) / 3.0;
        double bGain = avg / Math.Max(means.Val0, 1.0);
        double gGain = avg / Math.Max(means.Val1, 1.0);
        double rGain = avg / Math.Max(means.Val2, 1.0);

        var channels = Cv2.Split(bgr);
        try
        {
            channels[0].ConvertTo(channels[0], MatType.CV_8UC1, bGain);
            channels[1].ConvertTo(channels[1], MatType.CV_8UC1, gGain);
            channels[2].ConvertTo(channels[2], MatType.CV_8UC1, rGain);
            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

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
        var channels = Cv2.Split(hsv);
        Mat boosted;
        try
        {
            channels[1].ConvertTo(channels[1], MatType.CV_8UC1, 1.0 + amount);
            Cv2.Merge(channels, hsv);
            boosted = new Mat();
            Cv2.CvtColor(hsv, boosted, ColorConversionCodes.HSV2BGR);
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }

        using (boosted)
        {
            return BlendByAlpha(bgr, boosted, alpha);
        }
    }

    /// <summary>Composites <paramref name="inside"/> where alpha is high over <paramref name="outside"/> where alpha is low.</summary>
    private static Mat BlendByAlpha(Mat outside, Mat inside, Mat alpha)
    {
        using var alphaF = new Mat();
        alpha.ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);
        using var alpha3 = new Mat();
        Cv2.CvtColor(alphaF, alpha3, ColorConversionCodes.GRAY2BGR);

        using var outsideF = new Mat();
        outside.ConvertTo(outsideF, MatType.CV_32FC3);
        using var insideF = new Mat();
        inside.ConvertTo(insideF, MatType.CV_32FC3);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, inv);

        using var outsideW = outsideF.Mul(inv).ToMat();
        using var insideW = insideF.Mul(alpha3).ToMat();
        using var blended = (outsideW + insideW).ToMat();

        var result = new Mat();
        blended.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }
}
