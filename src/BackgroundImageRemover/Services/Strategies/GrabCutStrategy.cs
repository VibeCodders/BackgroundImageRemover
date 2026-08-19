using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Segments foreground/background using OpenCV's GrabCut. The subject can be seeded from any
/// combination of a user-drawn rectangle and foreground/background scribbles -- all three inputs
/// are optional on their own, but at least one is required to have anything to segment from.
/// </summary>
public sealed class GrabCutStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.GrabCut;

    // Holds the last raw GrabCut label mask (GC_BGD/GC_FGD/GC_PR_BGD/GC_PR_FGD per pixel) so a
    // higher-resolution run (the full-res export re-running the preview's strategy) can seed
    // from the preview's result instead of segmenting independently from scratch.
    // Two independent GrabCut runs at different resolutions can settle on visibly different
    // boundaries even with the same inputs, since the color-model statistics differ; seeding
    // keeps the full-res result a refinement of what the user saw, not a fresh guess.
    //
    // The strategy is a singleton shared by the debounced preview, full-res apply/export and
    // batch processing, which run on separate background threads. ComputeMask is serialized
    // with _sync so the cached label mask can never be disposed by one call while another is
    // still reading it (which previously surfaced as "Cannot access a disposed object").
    private readonly object _sync = new();
    private Mat? _lastLabelMask;
    private Size _lastLabelMaskSize;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        lock (_sync)
        {
            return ComputeMaskCore(bgr, context, ct);
        }
    }

    private Mat ComputeMaskCore(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        var rect = ClampRect(context.GrabCutRect, bgr);
        bool hasForeground = context.GrabCutForegroundScribble is not null;
        bool hasBackground = context.GrabCutBackgroundScribble is not null;
        bool hasScribbles = hasForeground || hasBackground;

        var gcMask = new Mat();
        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        if (_lastLabelMask is { } priorMask && (bgr.Width > _lastLabelMaskSize.Width || bgr.Height > _lastLabelMaskSize.Height))
        {
            // A higher-resolution call than the last one (the full-res export re-running the
            // preview's strategy): upscale the previous (lower-res) label mask -- nearest-neighbor,
            // since these are discrete labels, not intensities -- and use it as-is. Do NOT run
            // GrabCut again here: an extra refinement pass would let the full-res color-model
            // statistics pull the boundary away from what the preview showed, making the export
            // visibly different from the preview instead of just a higher-resolution version of it.
            Cv2.Resize(priorMask, gcMask, bgr.Size(), interpolation: InterpolationFlags.Nearest);
        }
        else if (rect is { } r)
        {
            Cv2.GrabCut(bgr, gcMask, r, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithRect);
        }
        else if (!hasScribbles)
        {
            // If no rectangle was drawn and no scribbles exist, default to the entire image
            // (with a 1px border margin required by OpenCV GrabCut)
            var fullRect = new Rect(1, 1, Math.Max(1, bgr.Width - 2), Math.Max(1, bgr.Height - 2));
            Cv2.GrabCut(bgr, gcMask, fullRect, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithRect);
        }
        else
        {
            // No rectangle, and nothing to continue from: start from an all-probable-background
            // mask and let the scribbles alone define the subject.
            gcMask.Create(bgr.Size(), MatType.CV_8UC1);
            gcMask.SetTo(new Scalar((byte)GrabCutMasks.GC_PR_BGD));
        }

        if (hasScribbles)
        {
            if (hasForeground)
            {
                gcMask.SetTo(new Scalar((byte)GrabCutMasks.GC_FGD), context.GrabCutForegroundScribble);
            }
            if (hasBackground)
            {
                gcMask.SetTo(new Scalar((byte)GrabCutMasks.GC_BGD), context.GrabCutBackgroundScribble);
            }
            Cv2.GrabCut(bgr, gcMask, default, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithMask);
        }

        _lastLabelMask?.Dispose();
        _lastLabelMask = gcMask;
        _lastLabelMaskSize = bgr.Size();

        return MaskFromLabels(gcMask, bgr.Size(), context.GrabCutFeatherPixels);
    }

    /// <summary>The raw GrabCut label mask from the last <see cref="ComputeMask"/> run, if any.</summary>
    public Mat? LastLabelMask => _lastLabelMask;

    // Clamp the rect to the Mat bounds -- a rect drawn on a differently-scaled preview could
    // otherwise fall slightly outside after coordinate mapping -- and treat a missing/degenerate
    // rect as "no rectangle" rather than an error, since it is now an optional input.
    private static Rect? ClampRect(Rect? rect, Mat bgr)
    {
        if (rect is not { } r || r.Width <= 0 || r.Height <= 0)
        {
            return null;
        }
        var clamped = r.Intersect(new Rect(0, 0, bgr.Width, bgr.Height));
        return clamped.Width > 0 && clamped.Height > 0 ? clamped : (Rect?)null;
    }

    private static Mat MaskFromLabels(Mat gcMask, Size size, int featherPixels)
    {
        using var binary = new Mat(size, MatType.CV_8UC1);
        Cv2.InRange(gcMask, new Scalar((byte)GrabCutMasks.GC_FGD), new Scalar((byte)GrabCutMasks.GC_FGD), binary);
        using var probableFgMask = new Mat(size, MatType.CV_8UC1);
        Cv2.InRange(gcMask, new Scalar((byte)GrabCutMasks.GC_PR_FGD), new Scalar((byte)GrabCutMasks.GC_PR_FGD), probableFgMask);
        Cv2.BitwiseOr(binary, probableFgMask, binary);

        // Kernel size scales with featherPixels (from the resolution-scaled context value) so
        // exports keep the same relative edge softness as the preview instead of a fixed pixel
        // amount that reads as much crisper at full resolution.
        int feather = Math.Max(1, featherPixels);
        int kernelSize = feather * 2 + 1;
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        var cleaned = new Mat();
        Cv2.MorphologyEx(binary, cleaned, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(cleaned, cleaned, MorphTypes.Close, kernel);

        var feathered = new Mat();
        Cv2.GaussianBlur(cleaned, feathered, new Size(kernelSize, kernelSize), 0);
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
