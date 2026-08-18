using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// High-performance vectorized image adjustments (Brightness, Contrast, Saturation, Hue shift, Blur, Sharpen)
/// implemented via OpenCvSharp.
/// </summary>
public static class ImageProcessingHelper
{
    /// <summary>
    /// Applies color and detail adjustments to a BGR image, returning a new BGR Mat.
    /// </summary>
    public static Mat ApplyAdjustments(Mat inputBgr, ImageAdjustments adjustments)
    {
        ArgumentNullException.ThrowIfNull(inputBgr);
        ArgumentNullException.ThrowIfNull(adjustments);

        if (adjustments.IsIdentity)
        {
            return inputBgr.Clone();
        }

        var current = inputBgr.Clone();

        try
        {
            // 1. Contrast and Brightness: new_pixel = alpha * old_pixel + beta
            if (Math.Abs(adjustments.Contrast - 1.0) > 1e-4 || Math.Abs(adjustments.Brightness) > 1e-4)
            {
                var adjusted = new Mat();
                current.ConvertTo(adjusted, MatType.CV_8UC3, adjustments.Contrast, adjustments.Brightness);
                current.Dispose();
                current = adjusted;
            }

            // 2. Saturation and Hue shift (in HSV space)
            if (Math.Abs(adjustments.Saturation - 1.0) > 1e-4 || Math.Abs(adjustments.HueShift) > 1e-4)
            {
                using var hsv = new Mat();
                Cv2.CvtColor(current, hsv, ColorConversionCodes.BGR2HSV);
                var channels = Cv2.Split(hsv);
                try
                {
                    // Hue is 0..180 in OpenCV 8-bit HSV
                    if (Math.Abs(adjustments.HueShift) > 1e-4)
                    {
                        // Convert degrees [-180, 180] to OpenCV hue scale [-90, 90]
                        double shift = (adjustments.HueShift / 360.0) * 180.0;
                        using var hFloat = new Mat();
                        channels[0].ConvertTo(hFloat, MatType.CV_32FC1);
                        Cv2.Add(hFloat, Scalar.All(shift), hFloat);

                        // Wrap modulo 180
                        using var shiftPositive = new Mat();
                        Cv2.Add(hFloat, Scalar.All(180.0), shiftPositive);
                        using var mod180 = new Mat();
                        // Compute (h + 180) % 180
                        // Since hFloat is in [-90, 270], shiftPositive is in [90, 450]
                        using var div = new Mat();
                        shiftPositive.ConvertTo(div, MatType.CV_32FC1, 1.0 / 180.0);
                        // Convert back to 8UC1 with modulo
                        for (int r = 0; r < channels[0].Rows; r++)
                        {
                            for (int c = 0; c < channels[0].Cols; c++)
                            {
                                float val = shiftPositive.At<float>(r, c) % 180f;
                                if (val < 0) val += 180f;
                                channels[0].Set(r, c, (byte)val);
                            }
                        }
                    }

                    // Saturation multiplier
                    if (Math.Abs(adjustments.Saturation - 1.0) > 1e-4)
                    {
                        channels[1].ConvertTo(channels[1], MatType.CV_8UC1, adjustments.Saturation);
                    }

                    Cv2.Merge(channels, hsv);
                    var bgrResult = new Mat();
                    Cv2.CvtColor(hsv, bgrResult, ColorConversionCodes.HSV2BGR);
                    current.Dispose();
                    current = bgrResult;
                }
                finally
                {
                    foreach (var ch in channels) ch.Dispose();
                }
            }

            // 3. Gaussian Blur
            if (adjustments.BlurRadius > 0)
            {
                int kSize = adjustments.BlurRadius * 2 + 1;
                var blurred = new Mat();
                Cv2.GaussianBlur(current, blurred, new Size(kSize, kSize), 0);
                current.Dispose();
                current = blurred;
            }

            // 4. Sharpen (Unsharp Mask)
            if (adjustments.SharpenStrength > 1e-4)
            {
                using var blurred = new Mat();
                Cv2.GaussianBlur(current, blurred, new Size(0, 0), 3);
                var sharpened = new Mat();
                // addWeighted: current * (1 + strength) + blurred * (-strength) + 0
                Cv2.AddWeighted(current, 1.0 + adjustments.SharpenStrength, blurred, -adjustments.SharpenStrength, 0, sharpened);
                current.Dispose();
                current = sharpened;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }
}
