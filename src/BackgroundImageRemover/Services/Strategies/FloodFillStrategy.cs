using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes a connected background region by flood-filling inward from the image border. Pixels
/// reachable from the border (within <see cref="StrategyContext.FloodFillTolerance"/> in Lab
/// space) are treated as background, so interior regions of a similar color — such as a white
/// shirt against a white wall — are preserved.
/// </summary>
public sealed class FloodFillStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.FloodFill;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        // Flood fill in Lab space so the tolerance behaves like a perceptual color distance.
        // Seed from the corners and edge midpoints so backgrounds that touch only part of the
        // border are still caught; all seeds add to the same background mask.
        Point[] seeds =
        {
            new(0, 0),
            new(bgr.Width - 1, 0),
            new(0, bgr.Height - 1),
            new(bgr.Width - 1, bgr.Height - 1),
            new(bgr.Width / 2, 0),
            new(bgr.Width / 2, bgr.Height - 1),
            new(0, bgr.Height / 2),
            new(bgr.Width - 1, bgr.Height / 2)
        };

        using var background = MaskHelpers.FloodFillBorderMask(bgr, seeds, context.FloodFillTolerance);
        ct.ThrowIfCancellationRequested();

        var mask = new Mat();
        Cv2.BitwiseNot(background, mask);
        return MaskHelpers.Feather(mask);
    }
}
