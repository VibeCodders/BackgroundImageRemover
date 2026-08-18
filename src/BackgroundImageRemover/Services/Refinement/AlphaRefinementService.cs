using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Non-destructive alpha-mask refinements applied to the whole cutout at once
/// (edge smoothing, feathering, speck removal and inversion). Each method returns a new Mat.
/// </summary>
public static class AlphaRefinementService
{
    /// <summary>Softens jagged edges with a median filter.</summary>
    public static Mat Smooth(Mat alpha, int kernelSize = 5)
    {
        int k = Math.Max(1, kernelSize) | 1; // force odd
        var result = new Mat();
        Cv2.MedianBlur(alpha, result, k);
        return result;
    }

    /// <summary>Feathers (blurs) the mask so the edge fades gradually.</summary>
    public static Mat Feather(Mat alpha, double sigma = 2.0)
    {
        sigma = Math.Max(0, sigma);
        var result = new Mat();
        Cv2.GaussianBlur(alpha, result, new Size(0, 0), sigma, sigma);
        return result;
    }

    /// <summary>Removes small foreground specks and small background holes via open + close.</summary>
    public static Mat RemoveSpecks(Mat alpha, int kernelSize = 3)
    {
        int k = Math.Max(1, kernelSize) | 1; // force odd
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
        using var opened = new Mat();
        Cv2.MorphologyEx(alpha, opened, MorphTypes.Open, kernel);
        var result = new Mat();
        Cv2.MorphologyEx(opened, result, MorphTypes.Close, kernel);
        return result;
    }

    /// <summary>Inverts the mask (foreground becomes background and vice versa).</summary>
    public static Mat Invert(Mat alpha)
    {
        var result = new Mat();
        Cv2.BitwiseNot(alpha, result);
        return result;
    }
}
