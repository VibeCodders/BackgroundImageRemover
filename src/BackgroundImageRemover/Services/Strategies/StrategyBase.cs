using System.Diagnostics;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Shared plumbing for strategies: both preview and full-res runs call the same
/// <see cref="ComputeMask"/> on a background thread and time the result; only the
/// input Mat's resolution differs between the two call sites. Optionally runs the
/// resulting mask through guided-filter alpha matting refinement.
/// </summary>
public abstract class StrategyBase : IBackgroundRemovalStrategy
{
    public abstract StrategyKind Kind { get; }

    /// <summary>Computes a single-channel 0-255 alpha mask (same size as <paramref name="bgr"/>).</summary>
    protected abstract Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct);

    /// <summary>Optional strategy-specific touch-up applied to the composited BGRA result, in-place.</summary>
    protected virtual void PostProcessBgra(Mat bgra, StrategyContext context)
    {
    }

    public Task<RemovalResult> RunPreviewAsync(Mat previewBgr, StrategyContext context, CancellationToken ct)
        => RunAsync(previewBgr, context, ct);

    public Task<RemovalResult> RunFullAsync(Mat fullBgr, StrategyContext context, CancellationToken ct)
        => RunAsync(fullBgr, context, ct);

    private Task<RemovalResult> RunAsync(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            using var rawMask = ComputeMask(bgr, context, ct);
            ct.ThrowIfCancellationRequested();

            using var mask = context.EnableAlphaMatting ? AlphaMattingRefiner.Refine(bgr, rawMask) : rawMask.Clone();

            // Global post-processing shared by every strategy: invert foreground/background,
            // clean up the mask, and optionally soften the edges before compositing into BGRA.
            if (context.InvertMask)
            {
                Cv2.BitwiseNot(mask, mask);
            }

            ApplyMaskCleanup(mask, context);

            if (context.MaskFeatherPixels > 0)
            {
                int kernelSize = context.MaskFeatherPixels * 2 + 1;
                using var feathered = new Mat();
                Cv2.GaussianBlur(mask, feathered, new Size(kernelSize, kernelSize), 0);
                feathered.CopyTo(mask);
            }

            var bgra = bgr.ToBgra(mask);

            PostProcessBgra(bgra, context);

            // The mask is feathered, so edge pixels still carry the original background color
            // in their RGB. Remove that cast so the exported cutout has no halo of the old image.
            if (context.DecontaminateEdges)
            {
                double despill = Math.Clamp(context.DespillStrength, 0.0, 1.0);
                if (despill > 1e-4)
                {
                    using var original = bgra.Clone();
                    ColorDecontaminator.Decontaminate(bgra, context.ChromaKeyColor, context.DecontaminationEstimateRadius);
                    if (despill < 1.0 - 1e-4)
                    {
                        Cv2.AddWeighted(original, 1.0 - despill, bgra, despill, 0, bgra);
                    }
                }
            }

            sw.Stop();
            return new RemovalResult(bgra, sw.Elapsed.TotalMilliseconds);
        }, ct);
    }

    /// <summary>Applies the shared mask-cleanup options (despeckle, fill holes, edge smoothing,
    /// keep largest component) to the mask in place.</summary>
    private static void ApplyMaskCleanup(Mat mask, StrategyContext context)
    {
        if (context.DespeckleKernelSize > 0)
        {
            int k = Math.Max(1, context.DespeckleKernelSize) | 1;
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        }

        if (context.FillHolesKernelSize > 0)
        {
            int k = Math.Max(1, context.FillHolesKernelSize) | 1;
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        }

        if (context.SmoothEdgesKernelSize > 0)
        {
            int k = Math.Max(1, context.SmoothEdgesKernelSize) | 1;
            Cv2.MedianBlur(mask, mask, k);
        }

        if (context.KeepLargestComponent)
        {
            KeepLargestComponent(mask);
        }

        if (context.MaskExpandPixels != 0)
        {
            ExpandOrShrink(mask, context.MaskExpandPixels);
        }

        if (context.MaskBlurPixels > 1e-4)
        {
            Cv2.GaussianBlur(mask, mask, new Size(0, 0), context.MaskBlurPixels, context.MaskBlurPixels);
        }

        if (context.MinComponentAreaPixels > 0)
        {
            RemoveSmallComponents(mask, context.MinComponentAreaPixels);
        }

        if (Math.Abs(context.MaskGamma - 1.0) > 1e-4)
        {
            mask.ApplyLut(i => 255.0 * Math.Pow(i / 255.0, Math.Clamp(context.MaskGamma, 0.1, 10.0)));
        }

        if (context.MaskHardness > 1e-4)
        {
            double h = Math.Clamp(context.MaskHardness, 0.0, 1.0);
            mask.ApplyLut(i =>
            {
                double t = i / 255.0;
                double hardened = t * t * (3.0 - 2.0 * t);
                return 255.0 * ((1.0 - h) * t + h * hardened);
            });
        }

        if (context.MaskThreshold > 0)
        {
            int threshold = Math.Clamp(context.MaskThreshold, 1, 255);
            mask.ApplyLut(i => i < threshold ? 0.0 : i);
        }

        if (context.MaskMedianKernel > 0)
        {
            int k = Math.Max(1, context.MaskMedianKernel) | 1;
            Cv2.MedianBlur(mask, mask, k);
        }

        if (context.MaskBilateralKernel > 0)
        {
            int d = Math.Max(1, context.MaskBilateralKernel) | 1;
            using var temp = new Mat();
            Cv2.BilateralFilter(mask, temp, d, d * 2.0, d / 2.0);
            temp.CopyTo(mask);
        }

        if (context.MaskClahe)
        {
            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
            clahe.Apply(mask, mask);
        }
    }

    /// <summary>Dilates (positive) or erodes (negative) the mask by the given pixel radius.</summary>
    private static void ExpandOrShrink(Mat mask, int pixels)
    {
        int k = Math.Max(1, Math.Abs(pixels)) | 1;
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
        if (pixels > 0)
        {
            Cv2.Dilate(mask, mask, kernel);
        }
        else
        {
            Cv2.Erode(mask, mask, kernel);
        }
    }

    /// <summary>Removes connected foreground components whose area is below <paramref name="minArea"/> pixels.</summary>
    private static void RemoveSmallComponents(Mat mask, int minArea)
    {
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int count = Cv2.ConnectedComponentsWithStats(
            mask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);
        if (count <= 1)
        {
            return;
        }

        using var keep = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.All(0));
        for (int i = 1; i < count; i++)
        {
            int area = stats.Get<int>(i, 4); // CC_STAT_AREA
            if (area >= minArea)
            {
                using var component = new Mat();
                Cv2.Compare(labels, new Scalar(i), component, CmpTypes.EQ);
                Cv2.BitwiseOr(keep, component, keep);
            }
        }

        Cv2.BitwiseAnd(mask, keep, mask);
    }

    /// <summary>Keeps only the largest connected foreground region (by area) in the mask.</summary>
    private static void KeepLargestComponent(Mat mask)
    {
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int count = Cv2.ConnectedComponentsWithStats(
            mask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);
        if (count <= 1)
        {
            return; // background only
        }

        int bestLabel = -1;
        int bestArea = 0;
        for (int i = 1; i < count; i++)
        {
            int area = stats.Get<int>(i, 4); // CC_STAT_AREA
            if (area > bestArea)
            {
                bestArea = area;
                bestLabel = i;
            }
        }

        if (bestLabel >= 0)
        {
            Cv2.Compare(labels, new Scalar(bestLabel), mask, CmpTypes.EQ);
        }
    }
}
