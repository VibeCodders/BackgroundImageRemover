using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared per-pixel iteration helpers. The nested <c>for (int y = 0; y &lt; rows; y++) /
/// for (int x = 0; x &lt; cols; x++)</c> skeleton with Mat Get/Set was copy-pasted across the
/// editing services (Duotone, Gradient, HueSat, Vignette, Wave, Liquify, Fx, Frame, OilPaint,
/// ColorReplace, CloneStamp, Noise). <see cref="ForEach"/> replaces just that skeleton; the
/// per-pixel body stays in the caller so each effect keeps its own math.
/// </summary>
public static class PixelLoop
{
    /// <summary>
    /// Runs <paramref name="action"/> once per pixel of <paramref name="mat"/> in row-major
    /// order, with the callback receiving (row, column).
    /// </summary>
    public static void ForEach(Mat mat, Action<int, int> action)
    {
        ArgumentNullException.ThrowIfNull(mat);
        ForEach(mat.Rows, mat.Cols, action);
    }

    /// <summary>
    /// Runs <paramref name="action"/> once per (row, column) pair of a
    /// <paramref name="rows"/> × <paramref name="cols"/> grid, in row-major order.
    /// </summary>
    public static void ForEach(int rows, int cols, Action<int, int> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                action(y, x);
            }
        }
    }
}
