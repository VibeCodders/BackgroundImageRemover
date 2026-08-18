using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Censorship effects (pixelation / blur) applied to a region or the whole image.</summary>
public static class MosaicService
{
    /// <summary>Pixelates a region (or the whole image when <paramref name="region"/> is null) with the given cell size.</summary>
    public static Mat Pixelate(Mat src, Rect? region, int cellSize)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        cellSize = Math.Max(1, cellSize);

        int smallW = Math.Max(1, bounds.Width / cellSize);
        int smallH = Math.Max(1, bounds.Height / cellSize);

        using var roi = new Mat(result, bounds);
        using var small = new Mat();
        Cv2.Resize(roi, small, new Size(smallW, smallH), interpolation: InterpolationFlags.Area);
        Cv2.Resize(small, roi, bounds.Size, interpolation: InterpolationFlags.Nearest);
        return result;
    }

    /// <summary>Blurs a region (or the whole image when <paramref name="region"/> is null) with the given radius.</summary>
    public static Mat Blur(Mat src, Rect? region, int radius)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        radius = Math.Max(1, radius);

        using var roi = new Mat(result, bounds);
        Cv2.GaussianBlur(roi, roi, new Size(0, 0), radius, radius);
        return result;
    }

    /// <summary>Applies a median blur (edge-preserving) to a region or the whole image.</summary>
    public static Mat MedianBlur(Mat src, Rect? region, int radius)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        radius = Math.Max(1, radius);
        int k = radius % 2 == 0 ? radius + 1 : radius;

        using var roi = new Mat(result, bounds);
        Cv2.MedianBlur(roi, roi, k);
        return result;
    }

    /// <summary>Fills a region (or the whole image) with a solid color.</summary>
    public static Mat SolidFill(Mat src, Rect? region, Vec3b color)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);

        using var roi = new Mat(result, bounds);
        roi.SetTo(new Scalar(color.Item0, color.Item1, color.Item2));
        return result;
    }

    /// <summary>Pixelates the whole image except the selected region (inverted censorship).</summary>
    public static Mat PixelateOutside(Mat src, Rect? region, int cellSize)
    {
        var result = Pixelate(src, null, cellSize);
        if (region is not { } r)
        {
            return result;
        }

        var bounds = Clamp(src.Size(), r);
        using var original = new Mat(src, bounds);
        using var dest = new Mat(result, bounds);
        original.CopyTo(dest);
        return result;
    }

    /// <summary>Pixelates with a jittered sample point per cell, producing a crystallized mosaic.</summary>
    public static Mat Crystallize(Mat src, Rect? region, int cellSize, int jitter)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        cellSize = Math.Max(1, cellSize);
        jitter = Math.Clamp(jitter, 0, cellSize - 1);

        using var roi = new Mat(result, bounds);
        int gw = Math.Max(1, (bounds.Width + cellSize - 1) / cellSize);
        int gh = Math.Max(1, (bounds.Height + cellSize - 1) / cellSize);
        var rng = new Random(12345);

        for (int gy = 0; gy < gh; gy++)
        {
            for (int gx = 0; gx < gw; gx++)
            {
                int cx0 = gx * cellSize;
                int cy0 = gy * cellSize;
                int sx = Math.Clamp(cx0 + jitter / 2 + (jitter > 0 ? rng.Next(jitter) : 0), 0, bounds.Width - 1);
                int sy = Math.Clamp(cy0 + jitter / 2 + (jitter > 0 ? rng.Next(jitter) : 0), 0, bounds.Height - 1);
                var color = roi.At<Vec3b>(sy, sx);
                int x1 = Math.Min(bounds.Width, cx0 + cellSize);
                int y1 = Math.Min(bounds.Height, cy0 + cellSize);
                using var cell = new Mat(roi, new Rect(cx0, cy0, x1 - cx0, y1 - cy0));
                cell.SetTo(new Scalar(color.Item0, color.Item1, color.Item2));
            }
        }

        return result;
    }

    /// <summary>Blurs a region with a partial strength (blend between the original and the blur).</summary>
    public static Mat BlurSoft(Mat src, Rect? region, int radius, double strength)
    {
        var result = src.Clone();
        var bounds = region is { } r ? Clamp(src.Size(), r) : new Rect(0, 0, src.Width, src.Height);
        radius = Math.Max(1, radius);
        strength = Math.Clamp(strength, 0.0, 1.0);

        using var roi = new Mat(result, bounds);
        using var blurred = new Mat();
        Cv2.GaussianBlur(roi, blurred, new Size(0, 0), radius, radius);
        if (strength >= 0.999)
        {
            blurred.CopyTo(roi);
            return result;
        }

        using var blurredF = new Mat();
        blurred.ConvertTo(blurredF, MatType.CV_32FC3);
        using var origF = new Mat();
        roi.ConvertTo(origF, MatType.CV_32FC3);
        using var blended = new Mat();
        Cv2.AddWeighted(origF, 1.0 - strength, blurredF, strength, 0.0, blended);
        blended.ConvertTo(roi, MatType.CV_8UC3);
        return result;
    }

    /// <summary>
    /// Blends <paramref name="modified"/> over <paramref name="original"/> using a single-channel
    /// mask (255 = modified, 0 = original), producing a soft-edged composite.
    /// </summary>
    public static Mat BlendByMask(Mat original, Mat modified, Mat mask)
    {
        var result = original.Clone();

        using var maskF = new Mat();
        mask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);
        using var mask3 = new Mat();
        Cv2.CvtColor(maskF, mask3, ColorConversionCodes.GRAY2BGR);

        using var origF = new Mat();
        original.ConvertTo(origF, MatType.CV_32FC3);
        using var modF = new Mat();
        modified.ConvertTo(modF, MatType.CV_32FC3);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(mask3.Size(), mask3.Type(), Scalar.All(1.0)), mask3, inv);
        using var origWeighted = origF.Mul(inv).ToMat();
        using var modWeighted = modF.Mul(mask3).ToMat();
        using var blended = (origWeighted + modWeighted).ToMat();
        blended.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }

    /// <summary>Clamps a region to the image bounds.</summary>
    public static Rect ClampRegion(Size size, Rect rect)
        => Clamp(size, rect);

    private static Rect Clamp(Size size, Rect rect)
    {
        int x = Math.Clamp(rect.X, 0, size.Width - 1);
        int y = Math.Clamp(rect.Y, 0, size.Height - 1);
        int width = Math.Clamp(rect.Width, 1, size.Width - x);
        int height = Math.Clamp(rect.Height, 1, size.Height - y);
        return new Rect(x, y, width, height);
    }
}
