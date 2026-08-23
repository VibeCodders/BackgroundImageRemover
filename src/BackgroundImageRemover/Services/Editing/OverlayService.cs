using BackgroundImageRemover.Helpers;
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
        // Use the raw (possibly negative) anchor position rather than AnchorPositionHelper's
        // pre-clamped intersection rect: BlendPrepared needs the true signed offset to crop the
        // correct portion of an overlay that is larger than the base image (see BlendPrepared).
        var pos = AnchorPositionHelper.ComputeOrigin(baseBgr.Size(), prepared.Size(), anchor, margin);

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
        // Resizing by fx/fy factors alone can round a small overlay down to a 0x0 target size
        // (e.g. a 10px overlay at the minimum 0.01 scale), which OpenCV rejects with an
        // "!dsize.empty()" exception. Compute an explicit target size clamped to at least 1x1.
        var targetSize = new Size(
            Math.Max(1, (int)Math.Round(overlay.Width * scale)),
            Math.Max(1, (int)Math.Round(overlay.Height * scale)));
        Cv2.Resize(overlay, current, targetSize, 0, 0, InterpolationFlags.Lanczos4);

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

        // x/y may be negative (e.g. an overlay larger than the base, anchored so part of it
        // falls off the left/top edge). Clamping x/y to 0 without also cropping the matching
        // amount off the overlay's origin used to paste the overlay's top-left corner at
        // canvas (0,0) instead of the correct, already-clipped, portion of the overlay.
        int destX = Math.Clamp(x, 0, baseBgr.Width);
        int destY = Math.Clamp(y, 0, baseBgr.Height);
        int srcX = destX - x;
        int srcY = destY - y;
        int w = Math.Min(overlay.Width - srcX, baseBgr.Width - destX);
        int h = Math.Min(overlay.Height - srcY, baseBgr.Height - destY);
        if (w <= 0 || h <= 0)
        {
            return result;
        }

        using var roi = new Mat(result, new Rect(destX, destY, w, h));
        using var overlayRoi = new Mat(overlay, new Rect(srcX, srcY, w, h));

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
        ImageProcessingUtility.AlphaComposite(roi, overlayRoi, opacity);
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
}
