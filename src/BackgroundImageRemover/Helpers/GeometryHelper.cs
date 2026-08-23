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
}
