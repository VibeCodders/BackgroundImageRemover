using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// High-performance vectorized image adjustments (Brightness, Contrast, Saturation, Hue shift, Temperature, Tint, Vignette, Blur, Sharpen)
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

            // 2. Temperature & Tint (RGB color balance shift)
            if (Math.Abs(adjustments.Temperature) > 1e-4 || Math.Abs(adjustments.Tint) > 1e-4)
            {
                var channels = Cv2.Split(current);
                try
                {
                    // Temperature: warm adds Red/decreases Blue, cool adds Blue/decreases Red
                    if (Math.Abs(adjustments.Temperature) > 1e-4)
                    {
                        double tempShift = adjustments.Temperature * 0.5; // [-50, 50]
                        Cv2.Add(channels[0], Scalar.All(-tempShift), channels[0]); // Blue channel
                        Cv2.Add(channels[2], Scalar.All(tempShift), channels[2]);  // Red channel
                    }

                    // Tint: positive adds Magenta (decreases Green), negative adds Green
                    if (Math.Abs(adjustments.Tint) > 1e-4)
                    {
                        double tintShift = adjustments.Tint * 0.5; // [-50, 50]
                        Cv2.Add(channels[1], Scalar.All(-tintShift), channels[1]); // Green channel
                    }

                    var balanced = new Mat();
                    Cv2.Merge(channels, balanced);
                    current.Dispose();
                    current = balanced;
                }
                finally
                {
                    foreach (var ch in channels) ch.Dispose();
                }
            }

            // 3. Saturation and Hue shift (in HSV space)
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

                        // Wrap modulo 180 safely: (h + 360) % 180
                        using var shiftPositive = new Mat();
                        Cv2.Add(hFloat, Scalar.All(360.0), shiftPositive);

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

            // 4. Gaussian Blur
            if (adjustments.BlurRadius > 0)
            {
                int kSize = adjustments.BlurRadius * 2 + 1;
                var blurred = new Mat();
                Cv2.GaussianBlur(current, blurred, new Size(kSize, kSize), 0);
                current.Dispose();
                current = blurred;
            }

            // 5. Sharpen (Unsharp Mask)
            if (adjustments.SharpenStrength > 1e-4)
            {
                using var blurred = new Mat();
                Cv2.GaussianBlur(current, blurred, new Size(0, 0), 3);
                var sharpened = new Mat();
                Cv2.AddWeighted(current, 1.0 + adjustments.SharpenStrength, blurred, -adjustments.SharpenStrength, 0, sharpened);
                current.Dispose();
                current = sharpened;
            }

            // 6. Vignette effect
            if (adjustments.Vignette > 1e-4)
            {
                using var vignetteMask = CreateVignetteMask(current.Size(), adjustments.Vignette);
                var vignetted = new Mat();
                using var currentFloat = new Mat();
                current.ConvertTo(currentFloat, MatType.CV_32FC3);

                var channels = Cv2.Split(currentFloat);
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Cv2.Multiply(channels[i], vignetteMask, channels[i]);
                    }
                    using var merged = new Mat();
                    Cv2.Merge(channels, merged);
                    merged.ConvertTo(vignetted, MatType.CV_8UC3);
                }
                finally
                {
                    foreach (var ch in channels) ch.Dispose();
                }

                current.Dispose();
                current = vignetted;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a smooth radial vignette intensity mask in [0..1] range as CV_32FC1.
    /// </summary>
    private static Mat CreateVignetteMask(Size size, double strength)
    {
        var mask = new Mat(size, MatType.CV_32FC1);
        float centerX = size.Width / 2.0f;
        float centerY = size.Height / 2.0f;
        float maxDistance = MathF.Sqrt(centerX * centerX + centerY * centerY);

        for (int r = 0; r < size.Height; r++)
        {
            float dy = r - centerY;
            for (int c = 0; c < size.Width; c++)
            {
                float dx = c - centerX;
                float dist = MathF.Sqrt(dx * dx + dy * dy) / maxDistance;
                // Cosine smooth roll-off
                float factor = 1.0f - (float)strength * MathF.Pow(dist, 1.8f);
                mask.Set(r, c, Math.Clamp(factor, 0.0f, 1.0f));
            }
        }

        return mask;
    }
}

