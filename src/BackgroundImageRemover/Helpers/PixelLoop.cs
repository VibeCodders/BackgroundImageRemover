using CommunityToolkit.HighPerformance;
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

    /// <summary>
    /// Wraps the Mat's native buffer as a zero-copy 2D view (<see cref="Span2D{T}"/> from
    /// CommunityToolkit.HighPerformance), indexed as <c>[row, column]</c>. No data is copied
    /// in or out — reads and writes hit the OpenCV memory directly — and the view honors the
    /// Mat's row stride, so non-continuous views (ROIs) work too. <typeparamref name="T"/> must
    /// match the Mat's element type (e.g. <see cref="Vec3b"/> for CV_8UC3, byte for CV_8UC1,
    /// float for CV_32FC1, <see cref="Vec4b"/> for CV_8UC4). The returned view is only valid
    /// while the Mat is alive; do not use it after the Mat is disposed.
    /// </summary>
    public static unsafe Span2D<T> AsSpan2D<T>(this Mat mat) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mat);
        if (mat.IsDisposed || mat.Empty())
        {
            return default;
        }

        int rows = mat.Rows;
        int cols = mat.Cols;
        long step = mat.Step();          // bytes per row (parent stride for ROI views)
        long elemSize = mat.ElemSize();  // bytes per element
        int padding = (int)((step / elemSize) - cols);
        return new Span2D<T>((T*)mat.DataPointer, rows, cols, padding);
    }
}
