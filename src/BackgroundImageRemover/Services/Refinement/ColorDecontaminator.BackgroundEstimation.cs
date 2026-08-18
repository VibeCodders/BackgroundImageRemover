using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Background color estimation utilities for color decontamination operations.
/// </summary>
internal static class BackgroundEstimation
{
    private const float DensityThreshold = 1e-4f;

    /// <summary>
    /// Estimates the background color at every pixel of the working region, returning BGR float
    /// Mats plus the estimation density. The estimate is a low-frequency field (a box filter of
    /// the mask), so on large regions it is computed at reduced resolution and upsampled back;
    /// small regions are computed exactly.
    /// </summary>
    public static (Mat B, Mat G, Mat R, Mat Density) EstimateBackground(
        Mat[] channelViews, Mat alphaView, int estimateRadius)
    {
        // Fully transparent (alpha == 0) pixels are known background; everything else is excluded.
        using var bgMask = new Mat();
        Cv2.Threshold(alphaView, bgMask, 0, 255, ThresholdTypes.BinaryInv);
        using var maskF = new Mat();
        bgMask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);

        int kernelSize = Math.Max(3, estimateRadius * 2 + 1);

        // Downscale so the low-resolution kernel stays at least 3x3, but never below 1/8 and
        // only when the region is genuinely large (small images stay pixel-exact).
        int scale = Math.Min(8, Math.Max(1, kernelSize / 3));
        bool downscale = scale > 1
            && alphaView.Width / scale >= 128
            && alphaView.Height / scale >= 128;

        using var workMask = new Mat();
        Size workSize = alphaView.Size();
        Size workKernel = new(kernelSize, kernelSize);
        if (downscale)
        {
            workSize = new Size(alphaView.Width / scale, alphaView.Height / scale);
            Cv2.Resize(maskF, workMask, workSize, interpolation: InterpolationFlags.Area);
            int smallKernel = Math.Max(3, kernelSize / scale);
            workKernel = new Size(smallKernel, smallKernel);
        }
        else
        {
            maskF.CopyTo(workMask);
        }

        // Normalized box filter of the mask = local fraction of background pixels (0..1).
        var density = new Mat();
        Cv2.BoxFilter(workMask, density, MatType.CV_32F, workKernel);

        // Local mean background color per channel = box(channel*mask) / box(mask).
        // Where the density is ~0 the division yields NaN/Inf, but those pixels are masked out later.
        var result = new Mat[3];
        for (int c = 0; c < 3; c++)
        {
            using var channelF = new Mat();
            if (downscale)
            {
                using var small = new Mat();
                Cv2.Resize(channelViews[c], small, workSize, interpolation: InterpolationFlags.Area);
                small.ConvertTo(channelF, MatType.CV_32FC1);
            }
            else
            {
                channelViews[c].ConvertTo(channelF, MatType.CV_32FC1);
            }

            using var weighted = channelF.Mul(workMask).ToMat();
            var numerator = new Mat();
            Cv2.BoxFilter(weighted, numerator, MatType.CV_32F, workKernel);
            var estimate = new Mat();
            Cv2.Divide(numerator, density, estimate);
            numerator.Dispose();

            if (downscale)
            {
                var fullSize = new Mat();
                Cv2.Resize(estimate, fullSize, alphaView.Size(), interpolation: InterpolationFlags.Linear);
                estimate.Dispose();
                estimate = fullSize;
            }

            result[c] = estimate;
        }

        if (downscale)
        {
            var fullDensity = new Mat();
            Cv2.Resize(density, fullDensity, alphaView.Size(), interpolation: InterpolationFlags.Linear);
            density.Dispose();
            density = fullDensity;
        }

        return (result[0], result[1], result[2], density);
    }

    /// <summary>
    /// Creates a density mask for valid background estimates.
    /// </summary>
    public static Mat CreateDensityMask(Mat density, Mat edgeMask)
    {
        using var densityMask = new Mat();
        Cv2.Compare(density, Scalar.All(DensityThreshold), densityMask, CmpType.GE);

        // Owned by the caller: must NOT be a using-declaration, otherwise it is disposed
        // before the method actually returns and the caller receives a dead Mat.
        var valid = new Mat();
        Cv2.BitwiseAnd(edgeMask, densityMask, valid);
        return valid;
    }
}