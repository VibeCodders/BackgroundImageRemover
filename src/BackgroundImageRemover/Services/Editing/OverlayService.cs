using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Composites a second image (logo/sticker) over a BGR base with scale, opacity and position.</summary>
public static class OverlayService
{
    public static Mat Composite(Mat baseBgr, Mat overlayBgra, TextAnchor anchor, double scale, double opacity, int margin)
        => Composite(baseBgr, overlayBgra, anchor, scale, opacity, margin, rotation: 0.0);

    public static Mat Composite(
        Mat baseBgr,
        Mat overlayBgra,
        TextAnchor anchor,
        double scale,
        double opacity,
        int margin,
        double rotation = 0.0,
        bool flipHorizontal = false,
        bool flipVertical = false,
        Vec3b? tint = null,
        bool dropShadow = false,
        int shadowOffset = 6,
        double shadowOpacity = 0.5,
        OverlayBlendMode blend = OverlayBlendMode.Normal)
    {
        scale = Math.Max(0.01, scale);
        opacity = Math.Clamp(opacity, 0.0, 1.0);
        margin = Math.Max(0, margin);

        using var prepared = PrepareOverlay(overlayBgra, scale, rotation, flipHorizontal, flipVertical, tint);
        var (pos, _) = ComputeIntersection(baseBgr.Size(), prepared.Size(), anchor, margin);

        Mat baseForOverlay;
        if (dropShadow)
        {
            baseForOverlay = ApplyDropShadow(baseBgr, prepared, new Point(pos.X, pos.Y), shadowOffset, shadowOpacity);
        }
        else
        {
            baseForOverlay = baseBgr.Clone();
        }

        using (baseForOverlay)
        {
            return BlendPrepared(baseForOverlay, prepared, pos.X, pos.Y, opacity, blend);
        }
    }

    private static Mat PrepareOverlay(Mat overlay, double scale, double rotation, bool flipH, bool flipV, Vec3b? tint)
    {
        var current = new Mat();
        Cv2.Resize(overlay, current, new Size(0, 0), scale, scale, InterpolationFlags.Lanczos4);

        try
        {
            if (flipH || flipV)
            {
                var mode = (flipH, flipV) switch
                {
                    (true, true) => FlipMode.XY,
                    (true, false) => FlipMode.Y,
                    _ => FlipMode.X
                };
                var flipped = new Mat();
                Cv2.Flip(current, flipped, mode);
                current.Dispose();
                current = flipped;
            }

            if (Math.Abs(rotation) > 1e-6)
            {
                var rotated = TransformService.Rotate(current, rotation);
                current.Dispose();
                current = rotated;
            }

            if (tint is { } t)
            {
                var channels = Cv2.Split(current);
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Cv2.Multiply(channels[i], new Scalar(t[i] / 255.0), channels[i]);
                    }
                    var tinted = new Mat();
                    Cv2.Merge(channels, tinted);
                    current.Dispose();
                    current = tinted;
                }
                finally
                {
                    foreach (var ch in channels) ch.Dispose();
                }
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private static Mat ApplyDropShadow(Mat baseBgr, Mat overlay, Point position, int offset, double shadowOpacity)
    {
        offset = Math.Max(0, offset);
        shadowOpacity = Math.Clamp(shadowOpacity, 0.0, 1.0);

        using var shadow = new Mat(overlay.Size(), MatType.CV_8UC4, Scalar.All(0));
        for (int y = 0; y < overlay.Height; y++)
        {
            for (int x = 0; x < overlay.Width; x++)
            {
                byte a = overlay.At<Vec4b>(y, x).Item3;
                byte sa = (byte)Math.Round(a * shadowOpacity);
                shadow.Set(y, x, new Vec4b(0, 0, 0, sa));
            }
        }

        int sx = Math.Clamp(position.X + offset, 0, baseBgr.Width);
        int sy = Math.Clamp(position.Y + offset, 0, baseBgr.Height);
        return BlendPrepared(baseBgr, shadow, sx, sy, 1.0, OverlayBlendMode.Normal);
    }

    private static Mat BlendPrepared(Mat baseBgr, Mat overlay, int x, int y, double opacity, OverlayBlendMode blend)
    {
        var result = baseBgr.Clone();
        x = Math.Clamp(x, 0, baseBgr.Width);
        y = Math.Clamp(y, 0, baseBgr.Height);
        int w = Math.Min(overlay.Width, baseBgr.Width - x);
        int h = Math.Min(overlay.Height, baseBgr.Height - y);
        if (w <= 0 || h <= 0)
        {
            return result;
        }

        using var roi = new Mat(result, new Rect(x, y, w, h));
        using var overlayRoi = new Mat(overlay, new Rect(0, 0, w, h));

        if (blend == OverlayBlendMode.Normal)
        {
            BlendNormal(roi, overlayRoi, opacity);
            return result;
        }

        for (int yy = 0; yy < h; yy++)
        {
            for (int xx = 0; xx < w; xx++)
            {
                var basePx = roi.At<Vec3b>(yy, xx);
                var fg = overlayRoi.At<Vec4b>(yy, xx);
                double a = fg.Item3 * opacity / 255.0;
                BlendPixel(basePx, new Vec3b(fg.Item0, fg.Item1, fg.Item2), blend, out var blended);
                roi.Set(yy, xx, new Vec3b(
                    (byte)Math.Round(basePx.Item0 * (1.0 - a) + blended.Item0 * a),
                    (byte)Math.Round(basePx.Item1 * (1.0 - a) + blended.Item1 * a),
                    (byte)Math.Round(basePx.Item2 * (1.0 - a) + blended.Item2 * a)));
            }
        }

        return result;
    }

    private static void BlendNormal(Mat roi, Mat overlayRoi, double opacity)
    {
        var channels = Cv2.Split(overlayRoi);
        try
        {
            using var alphaF = new Mat();
            channels[3].ConvertTo(alphaF, MatType.CV_32FC1, opacity / 255.0);

            using var overlayBgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, overlayBgr);

            using var overlayF = new Mat();
            overlayBgr.ConvertTo(overlayF, MatType.CV_32FC3);
            using var baseF = new Mat();
            roi.ConvertTo(baseF, MatType.CV_32FC3);

            using var alpha3 = new Mat();
            Cv2.CvtColor(alphaF, alpha3, ColorConversionCodes.GRAY2BGR);

            using var fgWeighted = overlayF.Mul(alpha3).ToMat();
            using var oneMinus = new Mat();
            Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, oneMinus);
            using var bgWeighted = baseF.Mul(oneMinus).ToMat();

            using var blended = (fgWeighted + bgWeighted).ToMat();
            blended.ConvertTo(roi, MatType.CV_8UC3);
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    private static void BlendPixel(Vec3b basePx, Vec3b fg, OverlayBlendMode blend, out Vec3b result)
    {
        switch (blend)
        {
            case OverlayBlendMode.Multiply:
                result = new Vec3b(
                    (byte)(basePx.Item0 * fg.Item0 / 255),
                    (byte)(basePx.Item1 * fg.Item1 / 255),
                    (byte)(basePx.Item2 * fg.Item2 / 255));
                break;
            case OverlayBlendMode.Screen:
                result = new Vec3b(
                    (byte)(255 - (255 - basePx.Item0) * (255 - fg.Item0) / 255),
                    (byte)(255 - (255 - basePx.Item1) * (255 - fg.Item1) / 255),
                    (byte)(255 - (255 - basePx.Item2) * (255 - fg.Item2) / 255));
                break;
            case OverlayBlendMode.Overlay:
                result = new Vec3b(
                    OverlayChannel(basePx.Item0, fg.Item0),
                    OverlayChannel(basePx.Item1, fg.Item1),
                    OverlayChannel(basePx.Item2, fg.Item2));
                break;
            default:
                result = fg;
                break;
        }
    }

    private static byte OverlayChannel(byte b, byte f)
    {
        if (b < 128)
        {
            return (byte)(2 * b * f / 255);
        }
        return (byte)(255 - 2 * (255 - b) * (255 - f) / 255);
    }

    private static (Rect Destination, Rect Overlay) ComputeIntersection(Size baseSize, Size overlaySize, TextAnchor anchor, int margin)
    {
        int x = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.MiddleLeft or TextAnchor.BottomLeft => margin,
            TextAnchor.TopCenter or TextAnchor.Center or TextAnchor.BottomCenter => (baseSize.Width - overlaySize.Width) / 2,
            _ => baseSize.Width - overlaySize.Width - margin
        };
        int y = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.TopCenter or TextAnchor.TopRight => margin,
            TextAnchor.MiddleLeft or TextAnchor.Center or TextAnchor.MiddleRight => (baseSize.Height - overlaySize.Height) / 2,
            _ => baseSize.Height - overlaySize.Height - margin
        };

        int ix0 = Math.Max(0, x);
        int iy0 = Math.Max(0, y);
        int ix1 = Math.Min(baseSize.Width, x + overlaySize.Width);
        int iy1 = Math.Min(baseSize.Height, y + overlaySize.Height);

        return (
            new Rect(ix0, iy0, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)),
            new Rect(ix0 - x, iy0 - y, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)));
    }
}
