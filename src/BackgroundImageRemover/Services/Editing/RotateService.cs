using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Arbitrary-angle rotation of an image Mat, optionally expanding the canvas so the
/// rotated result is never clipped.
/// </summary>
public static class RotateService
{
    /// <summary>
    /// Rotates <paramref name="input"/> by <paramref name="angle"/> degrees (positive =
    /// counter-clockwise, matching OpenCv's convention). When <paramref name="expand"/> is
    /// true the output canvas is grown to fully contain the rotated image; otherwise the
    /// output keeps the original dimensions and the corners are clipped to transparent.
    /// A null or empty input is returned as an empty Mat.
    /// </summary>
    public static Mat Rotate(Mat input, double angle, bool expand)
    {
        if (input is null || input.Empty())
        {
            return input.CloneOrEmpty();
        }

        if (Math.Abs(angle) < 1e-6)
        {
            return input.Clone();
        }

        // Fast path for exact 90° multiples with expand: use OpenCV's dedicated Rotate
        // which handles the dimension swap correctly without needing WarpAffine's
        // bounding-box math.
        if (expand)
        {
            double remainder = Math.Abs(angle % 360);
            if (Math.Abs(remainder - 90) < 1e-6)
            {
                var rotated = new Mat();
                Cv2.Rotate(input, rotated, RotateFlags.Rotate90Counterclockwise);
                return rotated;
            }
            if (Math.Abs(remainder - 180) < 1e-6)
            {
                var rotated = new Mat();
                Cv2.Rotate(input, rotated, RotateFlags.Rotate180);
                return rotated;
            }
            if (Math.Abs(remainder - 270) < 1e-6)
            {
                var rotated = new Mat();
                Cv2.Rotate(input, rotated, RotateFlags.Rotate90Clockwise);
                return rotated;
            }
        }

        double rad = angle * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        double absCos = Math.Abs(cos);
        double absSin = Math.Abs(sin);

        int srcW = input.Width;
        int srcH = input.Height;

        int dstW, dstH;
        double offsetX, offsetY;

        if (expand)
        {
            // Bounding box of the rotated image.
            dstW = Math.Max(1, (int)Math.Round(srcW * absCos + srcH * absSin));
            dstH = Math.Max(1, (int)Math.Round(srcW * absSin + srcH * absCos));

            // Translate the rotation so the rotated image is centered in the new canvas.
            offsetX = (dstW - srcW) / 2.0;
            offsetY = (dstH - srcH) / 2.0;
        }
        else
        {
            dstW = srcW;
            dstH = srcH;
            offsetX = 0.0;
            offsetY = 0.0;
        }

        var rotation = Cv2.GetRotationMatrix2D(
            new Point2f(srcW / 2.0f, srcH / 2.0f),
            angle,
            1.0);

        rotation.Set(0, 2, rotation.At<double>(0, 2) + offsetX);
        rotation.Set(1, 2, rotation.At<double>(1, 2) + offsetY);

        var result = new Mat();
        Cv2.WarpAffine(
            input,
            result,
            rotation,
            new Size(dstW, dstH),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            new Scalar(0, 0, 0, 0));

        return result;
    }
}
