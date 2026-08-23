using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class CloneStampService
{
    /// <summary>
    /// Copies pixels from the source position (destination pixel + <paramref name="sourceOffset"/>)
    /// wherever the <paramref name="mask"/> is non-zero. The mask's own intensity drives the blend
    /// (255 = full copy at <paramref name="opacity"/>, 0 = untouched, intermediate values feather),
    /// so the caller bakes brush softness/hardness into the mask. The mask must have the same size
    /// as <paramref name="bgr"/>. The caller owns the returned Mat.
    /// </summary>
    public static Mat CloneStamp(Mat bgr, Mat mask, Point sourceOffset, double opacity)
    {
        if (bgr is null || bgr.Empty() || mask is null || mask.Empty())
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(bgr);
        }

        opacity = Math.Clamp(opacity, 0.0, 1.0);
        var result = bgr.Clone();

        // Bulk array access: one copy in/out instead of native interop per pixel.
        byte[] maskData = PixelLoop.GetData<byte>(mask);
        Vec3b[] dstData = PixelLoop.GetData<Vec3b>(result);
        int cols = result.Cols;
        int rows = result.Rows;
        for (int i = 0; i < dstData.Length; i++)
        {
            byte maskValue = maskData[i];
            if (maskValue == 0)
            {
                continue;
            }

            int y = i / cols;
            int x = i % cols;
            int srcY = y - (int)sourceOffset.Y;
            int srcX = x - (int)sourceOffset.X;
            if (srcY < 0 || srcY >= rows || srcX < 0 || srcX >= cols)
            {
                continue;
            }

            double alpha = maskValue / 255.0 * opacity;
            Vec3b src = dstData[srcY * cols + srcX];
            Vec3b dst = dstData[i];
            dstData[i] = new Vec3b(
                (byte)Math.Round(dst[0] * (1 - alpha) + src[0] * alpha),
                (byte)Math.Round(dst[1] * (1 - alpha) + src[1] * alpha),
                (byte)Math.Round(dst[2] * (1 - alpha) + src[2] * alpha));
        }
        PixelLoop.SetData(result, dstData);

        return result;
    }
}
