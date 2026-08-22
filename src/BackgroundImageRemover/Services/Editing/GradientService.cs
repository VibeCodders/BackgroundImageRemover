using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Shape of the gradient overlay.</summary>
public enum GradientKind
{
    /// <summary>Blends along a straight axis across the image (angle-selectable).</summary>
    Linear,

    /// <summary>Blends radially outward from the image center.</summary>
    Radial
}

/// <summary>
/// Renders a smooth two-color gradient overlay onto the image. Used by the Gradient tool.
/// A linear gradient runs along a selectable axis; a radial gradient emanates from the center.
/// The overlay is blended over the source using the given opacity.
/// </summary>
public static class GradientService
{
    /// <summary>
    /// Applies a gradient overlay to <paramref name="bgr"/>. <paramref name="angleDeg"/> (in
    /// degrees, clockwise from pointing right) selects the axis for linear gradients and is
    /// ignored for radial ones. <paramref name="opacity"/> is 0..1 (0 = no effect).
    /// </summary>
    public static Mat Apply(Mat bgr, GradientKind kind, Vec3b colorA, Vec3b colorB, double angleDeg = 0, double opacity = 1.0)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (opacity <= EditingGuard.Epsilon)
        {
            return bgr.Clone();
        }

        int w = bgr.Width;
        int h = bgr.Height;
        double cx = (w - 1) / 2.0;
        double cy = (h - 1) / 2.0;
        double rad = angleDeg * Math.PI / 180.0;
        double dirX = Math.Cos(rad);
        double dirY = Math.Sin(rad);

        var mask = new Mat(h, w, MatType.CV_32FC1, Scalar.All(0));
        var overlay = new Mat(h, w, MatType.CV_8UC3);

        try
        {
            if (kind == GradientKind.Linear)
            {
                // Normalize by the largest corner projection so the gradient always spans the
                // full image regardless of angle.
                double maxProj = 0;
                foreach (double px in new[] { 0.0, w - 1.0 })
                {
                    foreach (double py in new[] { 0.0, h - 1.0 })
                    {
                        maxProj = Math.Max(maxProj, Math.Abs((px - cx) * dirX + (py - cy) * dirY));
                    }
                }
                if (maxProj < EditingGuard.Epsilon)
                {
                    maxProj = 1;
                }

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double proj = (x - cx) * dirX + (y - cy) * dirY;
                        double t = (proj / maxProj + 1.0) * 0.5;
                        WriteGradient(mask, overlay, x, y, Math.Clamp(t, 0.0, 1.0), opacity, colorA, colorB);
                    }
                }
            }
            else
            {
                double maxR = Math.Sqrt(w * w + h * h) / 2.0;
                if (maxR < EditingGuard.Epsilon)
                {
                    maxR = 1;
                }

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        WriteGradient(mask, overlay, x, y, Math.Clamp(dist / maxR, 0.0, 1.0), opacity, colorA, colorB);
                    }
                }
            }

            // Gravity-fill never fails below: BlendByMask builds a fresh Mat.
            var result = bgr.BlendByMask(overlay, mask);
            mask.Dispose();
            overlay.Dispose();
            return result;
        }
        catch
        {
            mask.Dispose();
            overlay.Dispose();
            throw;
        }
    }

    private static void WriteGradient(Mat mask, Mat overlay, int x, int y, double t, double opacity, Vec3b colorA, Vec3b colorB)
    {
        mask.Set(y, x, (float)(t * opacity));
        overlay.Set(y, x, new Vec3b(
            (byte)(colorA[0] + (colorB[0] - colorA[0]) * t),
            (byte)(colorA[1] + (colorB[1] - colorA[1]) * t),
            (byte)(colorA[2] + (colorB[2] - colorA[2]) * t)));
    }
}
