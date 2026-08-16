using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Selects a contiguous region of similar color around a seed point (flood fill) and applies
/// it to an existing alpha mask, in-place, as either an addition (restore to opaque) or a
/// removal (erase to transparent). Works on whatever mask/image resolution it's given.
/// </summary>
public static class MagicWandService
{
    public static void Apply(Mat bgr, Mat alpha, Point seed, double tolerance, bool add)
    {
        if (seed.X < 0 || seed.Y < 0 || seed.X >= bgr.Width || seed.Y >= bgr.Height)
        {
            return;
        }

        // FloodFill's mask must be 2px larger than the image on each side.
        using var floodMask = new Mat(bgr.Height + 2, bgr.Width + 2, MatType.CV_8UC1, Scalar.All(0));
        var diff = new Scalar(tolerance, tolerance, tolerance);

        using var bgrClone = bgr.Clone();
        Cv2.FloodFill(bgrClone, floodMask, seed, Scalar.All(255), out _, diff, diff,
            FloodFillFlags.Link4 | FloodFillFlags.MaskOnly | (FloodFillFlags)(255 << 8));

        using var regionMask = new Mat(floodMask, new Rect(1, 1, bgr.Width, bgr.Height));

        if (add)
        {
            alpha.SetTo(new Scalar(255), regionMask);
        }
        else
        {
            alpha.SetTo(new Scalar(0), regionMask);
        }
    }
}
