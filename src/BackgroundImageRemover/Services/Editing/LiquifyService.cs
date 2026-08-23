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

        // Each map entry is an independent per-pixel computation; RemapHelper fills the maps in
        // parallel and applies Cv2.Remap (constant border so warped pixels pull from black).
        float cx = center.X;
        float cy = center.Y;
        return RemapHelper.Remap(bgr, (x, y, mapXRow, mapYRow) =>
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
        }, BorderTypes.Constant, Scalar.All(0));
    }
}
