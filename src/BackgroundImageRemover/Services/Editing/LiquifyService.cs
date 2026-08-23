using System.Threading.Tasks;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Local warp effects for the Liquify tool. Each method distorts the pixels within a circular
/// region around a center point using a smooth falloff, implemented with <see cref="Cv2.Remap"/>.
/// </summary>
public static class LiquifyService
{
    public static Mat Warp(Mat bgr, Point center, double radius, double strength, LiquifyMode mode)
    {
        radius = Math.Max(1, radius);
        strength = Math.Clamp(strength, -2.0, 2.0);
        if (Math.Abs(strength) < 1e-4)
        {
            return bgr.Clone();
        }

        using var mapX = new Mat(bgr.Size(), MatType.CV_32FC1);
        using var mapY = new Mat(bgr.Size(), MatType.CV_32FC1);

        // Each map entry is an independent per-pixel computation, so rows are built in parallel.
        int w = bgr.Width;
        int h = bgr.Height;
        float cx = center.X;
        float cy = center.Y;
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
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float t = Math.Clamp(1.0f - dist / (float)radius, 0.0f, 1.0f);
                    float falloff = t * t * (3.0f - 2.0f * t); // smoothstep

                    float sx = x;
                    float sy = y;
                    switch (mode)
                    {
                        case LiquifyMode.Pinch:
                        {
                            float pull = (float)strength * falloff;
                            sx = cx + dx * (1.0f - pull);
                            sy = cy + dy * (1.0f - pull);
                            break;
                        }
                        case LiquifyMode.Bloat:
                        {
                            float push = (float)strength * falloff;
                            sx = cx + dx * (1.0f + push);
                            sy = cy + dy * (1.0f + push);
                            break;
                        }
                        case LiquifyMode.Twirl:
                        {
                            float angle = (float)strength * falloff;
                            float cos = MathF.Cos(angle);
                            float sin = MathF.Sin(angle);
                            float rx = dx * cos - dy * sin;
                            float ry = dx * sin + dy * cos;
                            sx = cx + rx;
                            sy = cy + ry;
                            break;
                        }
                        default:
                        {
                            float amount = (float)(strength * radius * falloff);
                            sx = x + (mode == LiquifyMode.PushLeft ? -amount : mode == LiquifyMode.PushRight ? amount : 0);
                            sy = y + (mode == LiquifyMode.PushUp ? -amount : mode == LiquifyMode.PushDown ? amount : 0);
                            break;
                        }
                    }

                    mapXRow[x] = sx;
                    mapYRow[x] = sy;
                }
            });
        }

        var result = new Mat();
        Cv2.Remap(bgr, result, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        return result;
    }
}
