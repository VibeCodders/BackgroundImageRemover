using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes the connected background region that shares a color with a user-clicked seed point.
/// The flood fill runs in Lab space so the tolerance behaves as a perceptual color distance,
/// and the cut edge is lightly feathered for an anti-aliased result.
/// </summary>
public sealed class MagicWandRemovalStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.MagicWand;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(255));

        var seed = context.MagicWandSeed;
        if (seed is not { } s || s.X < 0 || s.Y < 0 || s.X >= bgr.Width || s.Y >= bgr.Height)
        {
            return mask;
        }

        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);

        var diff = new Scalar(Math.Max(1, context.MagicWandTolerance));
        var flags = FloodFillFlags.Link8 | FloodFillFlags.MaskOnly | (FloodFillFlags)(255 << 8);

        // FloodFill's mask must be 2px larger than the image on each side.
        using var floodMask = new Mat(bgr.Height + 2, bgr.Width + 2, MatType.CV_8UC1, Scalar.All(0));
        Cv2.FloodFill(lab, floodMask, s, Scalar.All(255), out _, diff, diff, flags);
        ct.ThrowIfCancellationRequested();

        using var region = new Mat(floodMask, new Rect(1, 1, bgr.Width, bgr.Height));
        mask.SetTo(new Scalar(0), region);

        // The blurred mask is the strategy's output: ownership transfers to the caller, so it
        // must not be disposed here (unlike the `using` temporaries above).
        return MaskHelpers.Feather(mask);
    }
}
