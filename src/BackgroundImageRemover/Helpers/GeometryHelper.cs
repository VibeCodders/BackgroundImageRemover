using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Geometry utilities for rectangle and region operations shared across editing services.
/// </summary>
public static class GeometryHelper
{
    /// <summary>
    /// Clamps a rectangle to fit within the given image size.
    /// Returns a rectangle with positive width/height that lies entirely within bounds, or an
    /// empty rectangle when <paramref name="size"/> is empty (a degenerate image must not make
    /// the caller throw inside Math.Clamp).
    /// </summary>
    public static Rect ClampToSize(Size size, Rect rect)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        int x = Math.Clamp(rect.X, 0, size.Width - 1);
        int y = Math.Clamp(rect.Y, 0, size.Height - 1);
        int width = Math.Clamp(rect.Width, 1, size.Width - x);
        int height = Math.Clamp(rect.Height, 1, size.Height - y);
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// Computes the offset of point (x, y) from the axis-aligned rectangle
    /// [left, right] × [top, bottom] (edges inclusive): (0, 0) when the point is inside,
    /// otherwise the distance to the nearest edge along each axis (positive outside, 0 inside).
    /// Unifies the copy-pasted distance-to-interior math in the Uncrop fill services.
    /// </summary>
    public static void DistanceToRect(int x, int y, int left, int top, int right, int bottom, out int dx, out int dy)
    {
        dx = x < left ? left - x : x > right ? x - right : 0;
        dy = y < top ? top - y : y > bottom ? y - bottom : 0;
    }
}
