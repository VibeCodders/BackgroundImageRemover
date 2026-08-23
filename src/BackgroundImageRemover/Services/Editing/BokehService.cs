using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Decorative blurred circles (bokeh) overlaid on the image.</summary>
public static class BokehService
{
    /// <summary>
    /// Draws <paramref name="count"/> randomly placed circles of <paramref name="color"/> with a
    /// nominal <paramref name="radius"/> (each circle jitters between 40% and 100% of it), blurs
    /// the overlay by <paramref name="blurRadius"/> and blends it with <paramref name="opacity"/>.
    /// Positions are deterministic (fixed seed) so previews are stable across refreshes. The
    /// caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, Vec3b color, int radius, int count, double opacity, int blurRadius)
    {
        var result = bgr.Clone();
        if (count <= 0 || radius <= 0 || opacity <= 1e-4 || bgr.Width <= 2 || bgr.Height <= 2)
        {
            return result;
        }

        int w = bgr.Width;
        int h = bgr.Height;
        var rng = new Random(1337);

        using var overlay = new Mat(bgr.Size(), MatType.CV_8UC3, Scalar.All(0));
        var bgrColor = new Scalar(color.Item0, color.Item1, color.Item2);
        for (int i = 0; i < count; i++)
        {
            int r = Math.Max(1, (int)Math.Round(radius * (0.4 + 0.6 * rng.NextDouble())));
            int x = rng.Next(Math.Min(r, w - 1), Math.Max(r, w - 1) + 1);
            int y = rng.Next(Math.Min(r, h - 1), Math.Max(r, h - 1) + 1);
            Cv2.Circle(overlay, new Point(x, y), r, bgrColor, thickness: -1, LineTypes.AntiAlias);
        }

        // Circle mask: 255 where a circle was drawn, blurred for soft edges.
        using var mask = new Mat();
        Cv2.CvtColor(overlay, mask, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(mask, mask, 1, 255, ThresholdTypes.Binary);

        int blur = EditingGuard.EnsureOdd(blurRadius);
        if (blurRadius > 0)
        {
            Cv2.GaussianBlur(overlay, overlay, new OpenCvSharp.Size(blur, blur), 0);
            Cv2.GaussianBlur(mask, mask, new OpenCvSharp.Size(blur, blur), 0);
        }

        using var blended = new Mat();
        Cv2.AddWeighted(overlay, Math.Clamp(opacity, 0, 1), bgr, 1.0 - Math.Clamp(opacity, 0, 1), 0, blended);
        blended.CopyTo(result, mask);

        return result;
    }
}
