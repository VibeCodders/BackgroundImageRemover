using System.Threading.Tasks;
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

    /// <summary>
    /// Fills a single-channel CV_32FC1 Mat by invoking <paramref name="value"/> once per pixel
    /// in parallel (rows are independent, so results are identical to a sequential pass), writing
    /// straight into the native buffer through zero-copy spans. Replaces the unsafe
    /// <c>Parallel.For</c> + <c>Span&lt;float&gt;</c> mask-fill skeleton that was copy-pasted
    /// across the vignette/frame/adjustments services; the per-pixel math stays in the caller.
    /// </summary>
    public static unsafe void FillFloatParallel(Mat mask, Func<int, int, float> value)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(value);
        if (mask.IsDisposed || mask.Empty())
        {
            return;
        }

        int w = mask.Width;
        int h = mask.Height;
        byte* ptr = (byte*)mask.DataPointer;
        long step = mask.Step();
        Parallel.For(0, h, y =>
        {
            var row = new Span<float>((float*)(ptr + y * step), w);
            for (int x = 0; x < w; x++)
            {
                row[x] = value(x, y);
            }
        });
    }

    /// <summary>
    /// Runs <paramref name="rowAction"/> once per row of <paramref name="mat"/> in parallel
    /// (worker threads), passing the row's start address and its row index. Use inside an unsafe
    /// block and create a typed span from the pointer, e.g.
    /// <c>new Span&lt;Vec3b&gt;((Vec3b*)rowPtr, mat.Cols)</c>. The pointer honors the Mat's row
    /// stride, so ROI views work. Rows are processed by exactly one thread each and pixel math is
    /// deterministic, so results are identical to a sequential pass — this only makes CPU-bound
    /// per-pixel passes over large images faster. The returned spans are only valid for the
    /// duration of the callback; do not store them.
    /// </summary>
    public static unsafe void ForEachRowParallel(Mat mat, Action<IntPtr, int> rowAction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mat);
        ArgumentNullException.ThrowIfNull(rowAction);
        if (mat.IsDisposed || mat.Empty())
        {
            return;
        }

        byte* ptr = (byte*)mat.DataPointer;
        long step = mat.Step();
        Parallel.For(0, mat.Rows, new ParallelOptions { CancellationToken = ct }, y => rowAction((IntPtr)(ptr + y * step), y));
    }

    /// <summary>
    /// Runs <paramref name="rowAction"/> once per row in parallel over three Mats — two inputs
    /// and one destination (e.g. a blend pass) — passing the three row start addresses and the
    /// row index. The pointers honor each Mat's row stride, so ROI views work; rows are
    /// independent per-pixel computations, so results are identical to a sequential pass.
    /// Callers create typed spans from the pointers inside the callback. For an in-place pass
    /// (destination is also an input), pass the same Mat twice. Replaces the copy-pasted
    /// 3-pointer <c>Parallel.For</c> skeleton in the screen/alpha blend passes
    /// (<c>FxService.ScreenBlend</c>, <c>ImageProcessingUtility.CompositeOverBgra</c> and
    /// <c>AlphaComposite</c>).
    /// </summary>
    public static unsafe void ForEachRowParallel(Mat srcA, Mat srcB, Mat dst, Action<IntPtr, IntPtr, IntPtr, int> rowAction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcA);
        ArgumentNullException.ThrowIfNull(srcB);
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(rowAction);
        if (srcA.IsDisposed || srcA.Empty())
        {
            return;
        }

        byte* aPtr = (byte*)srcA.DataPointer;
        byte* bPtr = (byte*)srcB.DataPointer;
        byte* dPtr = (byte*)dst.DataPointer;
        long aStep = srcA.Step();
        long bStep = srcB.Step();
        long dStep = dst.Step();
        Parallel.For(0, srcA.Rows, new ParallelOptions { CancellationToken = ct }, y =>
            rowAction((IntPtr)(aPtr + y * aStep), (IntPtr)(bPtr + y * bStep), (IntPtr)(dPtr + y * dStep), y));
    }
}
