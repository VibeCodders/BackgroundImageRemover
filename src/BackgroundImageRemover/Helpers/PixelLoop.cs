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
    /// Copies the pixel data of <paramref name="mat"/> into a flat managed array in row-major
    /// order. Unlike <c>Mat.GetArray</c> this also works on non-continuous views (ROIs), which
    /// are cloned into a continuous buffer first. Avoids the per-pixel native interop of
    /// <c>Mat.Get&lt;T&gt;</c>/<c>Mat.At&lt;T&gt;</c> in hot per-pixel loops: one bulk copy
    /// instead of one call per pixel. The caller owns the returned array.
    /// </summary>
    public static T[] GetData<T>(Mat mat) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mat);
        if (mat.IsContinuous())
        {
            mat.GetArray(out T[] data);
            return data;
        }

        using var clone = mat.Clone();
        clone.GetArray(out T[] clonedData);
        return clonedData;
    }

    /// <summary>
    /// Writes a flat row-major array back into <paramref name="mat"/> (which must be
    /// continuous, i.e. not a sub-mat view). Companion to <see cref="GetData{T}"/>.
    /// </summary>
    public static void SetData<T>(Mat mat, T[] data) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mat);
        mat.SetArray(data);
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
