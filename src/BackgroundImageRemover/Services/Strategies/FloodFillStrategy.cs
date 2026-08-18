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
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);

        var background = new Mat(bgr.Height, bgr.Width, MatType.CV_8UC1, Scalar.All(0));
        var diff = new Scalar(Math.Max(1, context.FloodFillTolerance));

        // Seed from the corners and edge midpoints so backgrounds that touch only part of the
        // border are still caught. Each seed adds to the shared background mask.
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

        var flags = FloodFillFlags.Link8 | FloodFillFlags.MaskOnly | (FloodFillFlags)(255 << 8);
        foreach (var seed in seeds)
        {
            ct.ThrowIfCancellationRequested();

            // FloodFill's mask must be 2px larger than the image on every side.
            using var floodMask = new Mat(bgr.Height + 2, bgr.Width + 2, MatType.CV_8UC1, Scalar.All(0));
            Cv2.FloodFill(lab, floodMask, seed, Scalar.All(255), out _, diff, diff, flags);

            using var interior = new Mat(floodMask, new Rect(1, 1, bgr.Width, bgr.Height));
            Cv2.BitwiseOr(interior, background, background);
        }

        var mask = new Mat();
        Cv2.BitwiseNot(background, mask);
        background.Dispose();

        var feathered = new Mat();
        Cv2.GaussianBlur(mask, feathered, new Size(5, 5), 0);
        mask.Dispose();
        return feathered;
    }
}
