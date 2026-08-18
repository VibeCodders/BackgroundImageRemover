using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Paints a soft circular brush onto an alpha Mat, in-place. Consecutive stroke points are
/// interpolated (stamped at sub-radius spacing) so fast mouse movement leaves a continuous
/// stroke rather than isolated dots.
/// </summary>
public static class BrushEditor
{
    public static void StampSegment(Mat alpha, Point2f from, Point2f to, double radius, double hardness, BrushMode mode, double opacity = 1.0)
    {
        double distance = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        double step = Math.Max(1.0, radius * 0.35);
        int steps = Math.Max(1, (int)(distance / step));

        for (int i = 0; i <= steps; i++)
        {
            double t = steps == 0 ? 0 : (double)i / steps;
            var point = new Point2f(
                (float)(from.X + (to.X - from.X) * t),
                (float)(from.Y + (to.Y - from.Y) * t));
            StampPoint(alpha, point, radius, hardness, mode, opacity);
        }
    }

    private static void StampPoint(Mat alpha, Point2f center, double radius, double hardness, BrushMode mode, double opacity)
    {
        int r = Math.Max(1, (int)Math.Ceiling(radius));
        int cx = (int)Math.Round(center.X);
        int cy = (int)Math.Round(center.Y);

        int minX = Math.Max(0, cx - r);
        int minY = Math.Max(0, cy - r);
        int maxX = Math.Min(alpha.Width - 1, cx + r);
        int maxY = Math.Min(alpha.Height - 1, cy + r);
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        double hardRadius = radius * Math.Clamp(hardness, 0.0, 0.99);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt(Math.Pow(x - center.X, 2) + Math.Pow(y - center.Y, 2));
                if (dist > radius)
                {
                    continue;
                }

                double falloff = dist <= hardRadius
                    ? 1.0
                    : 1.0 - (dist - hardRadius) / Math.Max(1e-6, radius - hardRadius);
                falloff *= Math.Clamp(opacity, 0.0, 1.0);

                byte current = alpha.Get<byte>(y, x);
                byte updated = mode == BrushMode.Restore
                    ? (byte)Math.Clamp(current + falloff * 255, 0, 255)
                    : (byte)Math.Clamp(current - falloff * 255, 0, 255);
                alpha.Set(y, x, updated);
            }
        }
    }
}
