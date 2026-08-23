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

        using var background = MaskHelpers.FloodFillBorderMask(bgr, new[] { s }, context.MagicWandTolerance);
        ct.ThrowIfCancellationRequested();

        Cv2.BitwiseNot(background, mask);
        return MaskHelpers.Feather(mask);
    }
}
