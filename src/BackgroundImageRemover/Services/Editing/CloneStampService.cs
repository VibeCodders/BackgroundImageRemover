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

        PixelLoop.ForEach(result, (y, x) =>
        {
            byte maskValue = mask.Get<byte>(y, x);
            if (maskValue == 0)
            {
                return;
            }

            int srcY = y - (int)sourceOffset.Y;
            int srcX = x - (int)sourceOffset.X;
            if (srcY < 0 || srcY >= result.Rows || srcX < 0 || srcX >= result.Cols)
            {
                return;
            }

            double alpha = maskValue / 255.0 * opacity;
            Vec3b src = result.Get<Vec3b>(srcY, srcX);
            Vec3b dst = result.Get<Vec3b>(y, x);
            result.Set<Vec3b>(y, x, new Vec3b(
                (byte)Math.Round(dst[0] * (1 - alpha) + src[0] * alpha),
                (byte)Math.Round(dst[1] * (1 - alpha) + src[1] * alpha),
                (byte)Math.Round(dst[2] * (1 - alpha) + src[2] * alpha)));
        });

        return result;
    }
}
