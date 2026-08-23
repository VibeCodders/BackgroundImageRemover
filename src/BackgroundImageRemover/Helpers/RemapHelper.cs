using System.Threading.Tasks;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Builds and applies the float remap maps (mapX/mapY) used by the remap-based distortion
/// services (Wave, Liquify, FX chromatic aberration). The skeleton — allocate two CV_32FC1
/// maps, fill them in parallel over the native buffers, then <see cref="Cv2.Remap"/> — was
/// copy-pasted in each service; only the per-pixel destination math differs, and that stays
/// in the caller.
/// </summary>
public static class RemapHelper
{
    /// <summary>
    /// Creates a distortion result by filling float X/Y remap maps in parallel and applying
    /// <see cref="Cv2.Remap"/>. <paramref name="writeMaps"/> is invoked once per pixel with the
    /// mapX and mapY row spans already positioned at the current row; it writes the destination
    /// coordinates via <c>mapXRow[x]</c> / <c>mapYRow[x]</c>. Rows are independent per-pixel
    /// computations, so the fill runs on worker threads with results identical to a sequential
    /// pass. The caller owns the returned Mat.
    /// </summary>
    public static Mat Remap(
        Mat src,
        Action<int, int, Span<float>, Span<float>> writeMaps,
        BorderTypes border = BorderTypes.Replicate,
        Scalar? borderValue = null)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(writeMaps);

        int w = src.Width;
        int h = src.Height;
        using var mapX = new Mat(h, w, MatType.CV_32FC1);
        using var mapY = new Mat(h, w, MatType.CV_32FC1);
        unsafe
        {
            byte* xPtr = (byte*)mapX.DataPointer;
            byte* yPtr = (byte*)mapY.DataPointer;
            long xStep = mapX.Step();
            long yStep = mapY.Step();
            Parallel.For(0, h, y =>
            {
                var mapXRow = new Span<float>((float*)(xPtr + y * xStep), w);
                var mapYRow = new Span<float>((float*)(yPtr + y * yStep), w);
                for (int x = 0; x < w; x++)
                {
                    writeMaps(x, y, mapXRow, mapYRow);
                }
            });
        }

        var result = new Mat();
        if (borderValue is { } value)
        {
            Cv2.Remap(src, result, mapX, mapY, InterpolationFlags.Linear, border, value);
        }
        else
        {
            Cv2.Remap(src, result, mapX, mapY, InterpolationFlags.Linear, border);
        }

        return result;
    }
}
