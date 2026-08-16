using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Segments foreground/background using OpenCV's GrabCut, initialized from a
/// user-drawn rectangle around the subject.
/// </summary>
public sealed class GrabCutStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.GrabCut;

    private const int FeatherKernelSize = 5;

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

        using var gcMask = new Mat();
        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        Cv2.GrabCut(bgr, gcMask, rect, bgdModel, fgdModel, context.GrabCutIterations, GrabCutModes.InitWithRect);

        using var binary = new Mat(bgr.Size(), MatType.CV_8UC1);
        Cv2.InRange(gcMask, new Scalar((byte)GrabCutMasks.GC_FGD), new Scalar((byte)GrabCutMasks.GC_FGD), binary);
        using var probableFgMask = new Mat(bgr.Size(), MatType.CV_8UC1);
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
