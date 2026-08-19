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
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return bgr.Clone();
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(0, 0), 2, 2);
        using var sharpened = new Mat();
        // unsharp mask: result = original + strength * (original - blurred)
        Cv2.AddWeighted(bgr, 1.0 + strength, blurred, -strength, 0, sharpened);

        using var maskF = new Mat();
        mask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);
        using var mask3 = new Mat();
        Cv2.CvtColor(maskF, mask3, ColorConversionCodes.GRAY2BGR);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(mask3.Size(), mask3.Type(), Scalar.All(1.0)), mask3, inv);

        using var aF = new Mat();
        bgr.ConvertTo(aF, MatType.CV_32FC3);
        using var bF = new Mat();
        sharpened.ConvertTo(bF, MatType.CV_32FC3);

        using var aWeighted = aF.Mul(inv).ToMat();
        using var bWeighted = bF.Mul(mask3).ToMat();
        using var blended = (aWeighted + bWeighted).ToMat();

        var result = new Mat();
        blended.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }

    /// <summary>Sharps the entire image with an unsharp mask of <paramref name="strength"/> (0..1).</summary>
    public static Mat SharpenAll(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
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
