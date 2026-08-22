using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class CloneStampService
{
    public static Mat CloneStamp(Mat bgr, Mat mask, Point sourceOffset, double opacity, double hardness)
    {
        if (bgr is null || bgr.Empty() || mask is null || mask.Empty())
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(bgr);
        }

        var result = bgr.Clone();
        var brush = CreateBrushMask(mask.Size(), hardness);
        int brushRadius = brush.Width / 2;

        for (int y = 0; y < result.Rows; y++)
        {
            for (int x = 0; x < result.Cols; x++)
            {
                if (mask.Get<byte>(y, x) == 0) continue;

                int srcY = y - (int)sourceOffset.Y;
                int srcX = x - (int)sourceOffset.X;
                if (srcY < 0 || srcY >= result.Rows || srcX < 0 || srcX >= result.Cols) continue;

                int localY = y - (y - brushRadius);
                int localX = x - (x - brushRadius);
                if (localY < 0 || localY >= brush.Height || localX < 0 || localX >= brush.Width) continue;

                float alpha = brush.Get<float>(localY, localX) * (float)opacity;
                Vec3b src = result.Get<Vec3b>(srcY, srcX);
                Vec3b dst = result.Get<Vec3b>(y, x);
                Vec3b blended = new Vec3b(
                    (byte)Math.Clamp(dst[0] * (1 - alpha) + src[0] * alpha, 0, 255),
                    (byte)Math.Clamp(dst[1] * (1 - alpha) + src[1] * alpha, 0, 255),
                    (byte)Math.Clamp(dst[2] * (1 - alpha) + src[2] * alpha, 0, 255)
                );
                result.Set<Vec3b>(y, x, blended);
            }
        }

        return result;
    }

    private static Mat CreateBrushMask(Size size, double hardness)
    {
        int radius = Math.Min(size.Width, size.Height) / 2;
        if (radius <= 0) radius = 1;
        int diameter = radius * 2;
        var mask = new Mat(diameter, diameter, MatType.CV_32FC1, Scalar.All(0));
        int cx = radius;
        int cy = radius;
        double inner = radius * hardness;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                double dx = x - cx;
                double dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;

                float val;
                if (dist <= inner)
                {
                    val = 1.0f;
                }
                else
                {
                    double t = (dist - inner) / (radius - inner);
                    val = (float)(1.0 - t * t);
                }
                mask.Set<float>(y, x, val);
            }
        }
        return mask;
    }
}
