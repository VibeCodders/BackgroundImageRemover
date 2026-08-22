using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Geometric transforms on a BGR image. Every method returns a new Mat.</summary>
public static class TransformService
{
    public static Mat FlipHorizontal(Mat bgr)
    {
        var result = new Mat();
        Cv2.Flip(bgr, result, FlipMode.Y);
        return result;
    }

    public static Mat FlipVertical(Mat bgr)
    {
        var result = new Mat();
        Cv2.Flip(bgr, result, FlipMode.X);
        return result;
    }

    public static Mat Rotate90Clockwise(Mat bgr)
    {
        var result = new Mat();
        Cv2.Rotate(bgr, result, RotateFlags.Rotate90Clockwise);
        return result;
    }

    public static Mat Rotate90CounterClockwise(Mat bgr)
    {
        var result = new Mat();
        Cv2.Rotate(bgr, result, RotateFlags.Rotate90Counterclockwise);
        return result;
    }

    public static Mat Rotate180(Mat bgr)
    {
        var result = new Mat();
        Cv2.Rotate(bgr, result, RotateFlags.Rotate180);
        return result;
    }

    /// <summary>Rotates by an arbitrary angle (degrees, positive = counter-clockwise), expanding the canvas to fit.</summary>
    public static Mat Rotate(Mat bgr, double degrees)
    {
        if (Math.Abs(degrees) < 1e-6)
        {
            return bgr.Clone();
        }

        double rad = degrees * Math.PI / 180.0;
        double cos = Math.Abs(Math.Cos(rad));
        double sin = Math.Abs(Math.Sin(rad));
        int newWidth = (int)Math.Round(bgr.Width * cos + bgr.Height * sin);
        int newHeight = (int)Math.Round(bgr.Width * sin + bgr.Height * cos);

        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(bgr.Width / 2.0f, bgr.Height / 2.0f), degrees, 1.0);
        rotation.Set(0, 2, rotation.At<double>(0, 2) + (newWidth - bgr.Width) / 2.0);
        rotation.Set(1, 2, rotation.At<double>(1, 2) + (newHeight - bgr.Height) / 2.0);

        // Constant (transparent/black) border keeps the alpha channel correct when the input
        // is a BGRA cutout, while still expanding the canvas to fit the rotated image.
        var result = new Mat();
        Cv2.WarpAffine(bgr, result, rotation, new Size(newWidth, newHeight), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        return result;
    }

    /// <summary>Scales by a uniform factor; the factor is clamped to keep at least 1px in each dimension.</summary>
    public static Mat Resize(Mat bgr, double scale)
    {
        scale = Math.Max(scale, 1.0 / Math.Max(bgr.Width, bgr.Height));
        var result = new Mat();
        Cv2.Resize(bgr, result, new Size(0, 0), scale, scale, InterpolationFlags.Lanczos4);
        return result;
    }

    /// <summary>Resizes to exact pixel dimensions (stretching if the aspect ratio differs).</summary>
    public static Mat ResizeTo(Mat bgr, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var result = new Mat();
        Cv2.Resize(bgr, result, new Size(width, height), 0, 0, InterpolationFlags.Lanczos4);
        return result;
    }

    /// <summary>Shears the image horizontally (<paramref name="skewX"/>°) and/or vertically (<paramref name="skewY"/>°), expanding the canvas.</summary>
    public static Mat Skew(Mat bgr, double skewX, double skewY)
    {
        if (Math.Abs(skewX) < 1e-6 && Math.Abs(skewY) < 1e-6)
        {
            return bgr.Clone();
        }

        double tx = Math.Tan(skewX * Math.PI / 180.0);
        double ty = Math.Tan(skewY * Math.PI / 180.0);
        int newWidth = bgr.Width + Math.Max(0, (int)Math.Round(Math.Abs(tx) * bgr.Height));
        int newHeight = bgr.Height + Math.Max(0, (int)Math.Round(Math.Abs(ty) * bgr.Width));

        using var matrix = new Mat(2, 3, MatType.CV_64FC1);
        matrix.Set(0, 0, 1.0);
        matrix.Set(0, 1, tx);
        matrix.Set(0, 2, tx < 0 ? -tx * bgr.Height : 0.0);
        matrix.Set(1, 0, ty);
        matrix.Set(1, 1, 1.0);
        matrix.Set(1, 2, ty < 0 ? -ty * bgr.Width : 0.0);

        var result = new Mat();
        Cv2.WarpAffine(bgr, result, matrix, new Size(newWidth, newHeight), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        return result;
    }

    /// <summary>Crops to the largest centered rectangle with the requested width/height ratio.</summary>
    public static Mat CropToAspect(Mat bgr, double ratio)
    {
        var rect = CropService.CenteredRectForAspect(bgr.Size(), ratio);
        return CropService.CropRect(bgr, rect);
    }

    /// <summary>Trims away a near-uniform border (letterbox bars / flat backdrop), preserving any alpha channel.</summary>
    public static Mat TrimBorder(Mat img, int tolerance = 12)
    {
        Rect bounds;
        if (img.Channels() == 4)
        {
            var channels = Cv2.Split(img);
            try
            {
                using var bgr = new Mat();
                Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
                bounds = CropService.TrimContentBounds(bgr, tolerance);
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
        }
        else
        {
            bounds = CropService.TrimContentBounds(img, tolerance);
        }

        return CropService.CropRect(img, bounds);
    }

    /// <summary>Expands the canvas by the given per-side padding, filling the new area with <paramref name="fill"/>.</summary>
    public static Mat Pad(Mat img, int left, int top, int right, int bottom, Scalar fill)
    {
        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);

        int newWidth = img.Width + left + right;
        int newHeight = img.Height + top + bottom;
        var result = new Mat(newHeight, newWidth, img.Type(), fill);
        using var dst = new Mat(result, new Rect(left, top, img.Width, img.Height));
        img.CopyTo(dst);
        return result;
    }

    /// <summary>Resizes to fit inside the given box while preserving aspect ratio (never upscales above the box).</summary>
    public static Mat ResizeToFit(Mat img, int maxWidth, int maxHeight)
    {
        maxWidth = Math.Max(1, maxWidth);
        maxHeight = Math.Max(1, maxHeight);
        double scale = Math.Min((double)maxWidth / img.Width, (double)maxHeight / img.Height);
        scale = Math.Min(1.0, scale);
        return Resize(img, scale);
    }

    /// <summary>Crops a centered rectangle of the exact requested size (padded with <paramref name="fill"/> if the image is smaller).</summary>
    public static Mat CropCenter(Mat img, int width, int height, Scalar fill)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var result = new Mat(height, width, img.Type(), fill);
        int x = (img.Width - width) / 2;
        int y = (img.Height - height) / 2;
        int srcX = Math.Max(0, x);
        int srcY = Math.Max(0, y);
        int copyW = Math.Min(width, img.Width);
        int copyH = Math.Min(height, img.Height);
        int dstX = Math.Max(0, -x);
        int dstY = Math.Max(0, -y);

        using var src = CropService.CropRect(img, new Rect(srcX, srcY, copyW, copyH));
        using var dst = new Mat(result, new Rect(dstX, dstY, copyW, copyH));
        src.CopyTo(dst);
        return result;
    }

    /// <summary>Repeats (tiles) the image to fill a canvas of the exact requested size.</summary>
    public static Mat Tile(Mat img, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var result = new Mat(height, width, img.Type());
        for (int y = 0; y < height; y += img.Height)
        {
            for (int x = 0; x < width; x += img.Width)
            {
                int copyW = Math.Min(img.Width, width - x);
                int copyH = Math.Min(img.Height, height - y);
                if (copyW <= 0 || copyH <= 0) break;
                using var src = CropService.CropRect(img, new Rect(0, 0, copyW, copyH));
                using var dst = new Mat(result, new Rect(x, y, copyW, copyH));
                src.CopyTo(dst);
            }
        }
        return result;
    }

    /// <summary>
    /// Estimates the dominant skew angle (degrees, positive = clockwise) using Hough line detection.
    /// Returns 0 when no confident near-horizontal/vertical line is found.
    /// </summary>
    public static double EstimateSkewAngle(Mat img, int maxAngle = 30)
    {
        using var gray = ToGray(img);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        var lines = Cv2.HoughLinesP(edges, 1.0, Math.PI / 180.0, 80, 100, 10);

        var angles = new List<double>();
        foreach (var line in lines)
        {
            double dx = line.P2.X - line.P1.X;
            double dy = line.P2.Y - line.P1.Y;
            if (Math.Abs(dx) < 1e-6) continue;

            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (Math.Abs(angle) <= maxAngle)
            {
                angles.Add(angle);
            }
            else if (Math.Abs(Math.Abs(angle) - 90.0) <= maxAngle)
            {
                angles.Add(angle - Math.Sign(angle) * 90.0);
            }
        }

        if (angles.Count == 0)
        {
            return 0.0;
        }

        angles.Sort();
        return angles[angles.Count / 2];
    }

    /// <summary>Detects the dominant near-horizontal/near-vertical line orientation and rotates the image to straighten it.</summary>
    public static Mat AutoStraighten(Mat img, int maxAngle = 30)
    {
        double median = EstimateSkewAngle(img, maxAngle);
        if (Math.Abs(median) < 1e-6)
        {
            return img.Clone();
        }

        return Rotate(img, -median);
    }

    private static Mat ToGray(Mat img)
        => img.ToGrayBgr();
}
