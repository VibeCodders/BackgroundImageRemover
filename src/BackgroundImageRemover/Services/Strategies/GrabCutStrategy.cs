using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Segments foreground/background using OpenCV's GrabCut, initialized from a
/// user-drawn rectangle around the subject. Supports an optional second pass that refines
/// the result using foreground/background scribbles on top of the previous label mask.
/// </summary>
public sealed class GrabCutStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.GrabCut;

    private const int FeatherKernelSize = 5;

    // Holds the last raw GrabCut label mask (GC_BGD/GC_FGD/GC_PR_BGD/GC_PR_FGD per pixel) so a
    // subsequent scribble-refine pass can build on it instead of starting over from the rect,
    // and so a higher-resolution run (the full-res export re-running the preview's strategy)
    // can seed from the preview's result instead of segmenting independently from scratch.
    // Two independent GrabCut runs at different resolutions can settle on visibly different
    // boundaries even with the same rect, since the color-model statistics differ; seeding
    // keeps the full-res result a refinement of what the user saw, not a fresh guess.
    // The desktop app serializes preview/apply calls per strategy, so a single-instance cache
    // is safe in practice; a concurrent call would simply see a stale/overwritten mask.
    private Mat? _lastLabelMask;
    private Size _lastLabelMaskSize;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        if (context.GrabCutRect is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("GrabCut requires a subject rectangle.");
        }

        // Clamp the rect to the Mat bounds; a rect drawn on a differently-scaled preview
        // could otherwise fall slightly outside after coordinate mapping.
        rect = rect.Intersect(new Rect(0, 0, bgr.Width, bgr.Height));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("GrabCut rectangle does not overlap the image.");
        }

        var gcMask = new Mat();
        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        if (_lastLabelMask is { } priorMask && (bgr.Width > _lastLabelMaskSize.Width || bgr.Height > _lastLabelMaskSize.Height))
        {
            // A higher-resolution call than the last one: upscale the previous (lower-res)
            // label mask -- nearest-neighbor, since these are discrete labels, not intensities
            // -- and refine it in place instead of starting over from the rect.
            Cv2.Resize(priorMask, gcMask, bgr.Size(), interpolation: InterpolationFlags.Nearest);
            Cv2.GrabCut(bgr, gcMask, default, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithMask);
        }
        else
        {
            Cv2.GrabCut(bgr, gcMask, rect, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithRect);
        }

        _lastLabelMask?.Dispose();
        _lastLabelMask = gcMask;
        _lastLabelMaskSize = bgr.Size();

        return MaskFromLabels(gcMask, bgr.Size());
    }

    /// <summary>The raw GrabCut label mask from the last <see cref="ComputeMask"/> run, if any.</summary>
    public Mat? LastLabelMask => _lastLabelMask;

    /// <summary>
    /// Re-runs GrabCut starting from <paramref name="priorLabelMask"/>, overlaying the user's
    /// scribbles as certain foreground/background labels first. Returns the refined alpha mask
    /// and updates <see cref="LastLabelMask"/>.
    /// </summary>
    public Mat RefineWithScribbles(Mat bgr, Mat priorLabelMask, Mat? foregroundScribble, Mat? backgroundScribble, int iterations)
    {
        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        var workingMask = priorLabelMask.Clone();
        if (foregroundScribble is not null)
        {
            workingMask.SetTo(new Scalar((byte)GrabCutMasks.GC_FGD), foregroundScribble);
        }
        if (backgroundScribble is not null)
        {
            workingMask.SetTo(new Scalar((byte)GrabCutMasks.GC_BGD), backgroundScribble);
        }

        Cv2.GrabCut(bgr, workingMask, default, bgdModel, fgdModel, iterations, GrabCutModes.InitWithMask);

        _lastLabelMask?.Dispose();
        _lastLabelMask = workingMask;
        _lastLabelMaskSize = bgr.Size();

        return MaskFromLabels(workingMask, bgr.Size());
    }

    private static Mat MaskFromLabels(Mat gcMask, Size size)
    {
        using var binary = new Mat(size, MatType.CV_8UC1);
        Cv2.InRange(gcMask, new Scalar((byte)GrabCutMasks.GC_FGD), new Scalar((byte)GrabCutMasks.GC_FGD), binary);
        using var probableFgMask = new Mat(size, MatType.CV_8UC1);
        Cv2.InRange(gcMask, new Scalar((byte)GrabCutMasks.GC_PR_FGD), new Scalar((byte)GrabCutMasks.GC_PR_FGD), probableFgMask);
        Cv2.BitwiseOr(binary, probableFgMask, binary);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(FeatherKernelSize, FeatherKernelSize));
        var cleaned = new Mat();
        Cv2.MorphologyEx(binary, cleaned, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(cleaned, cleaned, MorphTypes.Close, kernel);

        var feathered = new Mat();
        Cv2.GaussianBlur(cleaned, feathered, new Size(FeatherKernelSize, FeatherKernelSize), 0);
        cleaned.Dispose();

        return feathered;
    }

    private enum GrabCutMasks : byte
    {
        GC_BGD = 0,
        GC_FGD = 1,
        GC_PR_BGD = 2,
        GC_PR_FGD = 3
    }
}
