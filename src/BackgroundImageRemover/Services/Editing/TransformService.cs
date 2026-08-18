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
}
