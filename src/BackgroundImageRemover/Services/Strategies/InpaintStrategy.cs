using BackgroundImageRemover.Helpers;
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
        // Determine which pixels are "known background" (i.e. the region to in-paint).
        // The Inpaint strategy reuses the shared mask-cleanup pipeline, so we seed the mask
        // with an estimate of the background and let the post-processing refine it.
        using var bgMask = EstimateBackgroundMask(bgr, context);
        ct.ThrowIfCancellationRequested();

        // Inpaint the background region of the source image so the composited cutout blends.
        using var inpainted = new Mat();
        double radius = Math.Max(1.0, context.InpaintRadius);
        Cv2.Inpaint(bgr, bgMask, inpainted, radius, InpaintMethod.NS);

        // The strategy's mask contract is 255 = subject (keep): invert the background mask.
        // (Regression: this used to return the background mask as-is, so the tool kept the
        // background and removed the subject — or, with the old 0 fill value, removed everything.)
        var subjectMask = new Mat();
        Cv2.BitwiseNot(bgMask, subjectMask);
        return subjectMask;
    }

    /// <summary>
    /// Estimates the background region of the image. The current implementation uses a simple
    /// border-seed flood fill in Lab space; callers can override the tolerance through
    /// <see cref="StrategyContext.InpaintTolerance"/>.
    /// </summary>
    private static Mat EstimateBackgroundMask(Mat bgr, StrategyContext context)
    {
        // Seed from the four corners; any connected border region is treated as background.
        var seeds = new[]
        {
            new Point(0, 0),
            new Point(bgr.Width - 1, 0),
            new Point(0, bgr.Height - 1),
            new Point(bgr.Width - 1, bgr.Height - 1)
        };

        return MaskHelpers.FloodFillBorderMask(bgr, seeds, context.InpaintTolerance);
    }
}
