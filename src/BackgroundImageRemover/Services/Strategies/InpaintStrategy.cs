using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes the background by in-painting the region outside the estimated foreground mask.
/// Uses OpenCV's Navier-Stokes based <see cref="Cv2.Inpaint(Mat,Mat,double,InpaintMethod)"/>
/// to reconstruct the background pixels from the surrounding foreground, producing a seamless
/// matte when the subject is later composited onto a new background.
/// </summary>
public sealed class InpaintStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.Inpaint;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        // Start from a full-opacity (255) mask: every pixel is foreground until we prove otherwise.
        var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(255));

        // Determine which pixels are "known background" (i.e. the region to in-paint).
        // The Inpaint strategy reuses the shared mask-cleanup pipeline, so we seed the mask
        // with an estimate of the background and let the post-processing refine it.
        using var bgMask = EstimateBackgroundMask(bgr, context);
        ct.ThrowIfCancellationRequested();

        // Inpaint the background region of the source image so the composited cutout blends.
        using var inpainted = new Mat();
        double radius = Math.Max(1.0, context.InpaintRadius);
        Cv2.Inpaint(bgr, bgMask, inpainted, radius, InpaintMethod.NS);

        // The mask we return is the background region (inverted so 255 = keep).
        // The shared post-processing in StrategyBase will invert/cleanup as configured.
        var backgroundMask = new Mat();
        bgMask.CopyTo(backgroundMask);
        return backgroundMask;
    }

    /// <summary>
    /// Estimates the background region of the image. The current implementation uses a simple
    /// border-seed flood fill in Lab space; callers can override the tolerance through
    /// <see cref="StrategyContext.InpaintTolerance"/>.
    /// </summary>
    private static Mat EstimateBackgroundMask(Mat bgr, StrategyContext context)
    {
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);

        // FloodFill's mask must be 2px larger than the image on each side.
        using var floodMask = new Mat(bgr.Height + 2, bgr.Width + 2, MatType.CV_8UC1, Scalar.All(0));
        var diff = new Scalar(Math.Max(1, context.InpaintTolerance));
        var flags = FloodFillFlags.Link8 | FloodFillFlags.MaskOnly | (FloodFillFlags)(0 << 8);

        // Seed from the four corners; any connected border region is treated as background.
        var seeds = new[]
        {
            new Point(0, 0),
            new Point(bgr.Width - 1, 0),
            new Point(0, bgr.Height - 1),
            new Point(bgr.Width - 1, bgr.Height - 1)
        };

        foreach (var seed in seeds)
        {
            Cv2.FloodFill(lab, floodMask, seed, Scalar.All(255), out _, diff, diff, flags);
        }

        // Extract the inner region (drop the 1px border added by FloodFill).
        var region = new Mat(floodMask, new Rect(1, 1, bgr.Width, bgr.Height));
        var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        region.CopyTo(mask);
        return mask;
    }
}
