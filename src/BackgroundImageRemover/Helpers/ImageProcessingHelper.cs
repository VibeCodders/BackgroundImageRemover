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
            // 0. One-click auto enhancement (CLAHE contrast + gray-world white balance).
            if (adjustments.AutoEnhance)
            {
                var enhanced = ApplyAutoEnhance(current);
                current.Dispose();
                current = enhanced;
            }

            // 1. Contrast and Brightness: new_pixel = alpha * old_pixel + beta
            if (Math.Abs(adjustments.Contrast - 1.0) > 1e-4 || Math.Abs(adjustments.Brightness) > 1e-4)
            {
                var adjusted = new Mat();
                current.ConvertTo(adjusted, MatType.CV_8UC3, adjustments.Contrast, adjustments.Brightness);
                current.Dispose();
                current = adjusted;
            }

            // 1.5 Exposure (gamma curve).
            if (Math.Abs(adjustments.Exposure - 1.0) > 1e-4)
            {
                using var lut = ImageProcessingUtility.BuildLut(i => 255.0 * Math.Pow(i / 255.0, 1.0 / adjustments.Exposure));
                var adjusted = new Mat();
                Cv2.LUT(current, lut, adjusted);
                current.Dispose();
                current = adjusted;
            }

            // 1.6 Shadow lift and highlight recovery.
            if (Math.Abs(adjustments.Highlights) > 1e-4 || Math.Abs(adjustments.Shadows) > 1e-4)
            {
                double shadows = adjustments.Shadows;
                double highlights = adjustments.Highlights;
                using var lut = ImageProcessingUtility.BuildLut(i =>
                {
                    double v = i;
                    v += shadows * 0.5 * (1.0 - v / 255.0);
                    v -= highlights * 0.5 * (v / 255.0);
                    return v;
                });
                var adjusted = new Mat();
                Cv2.LUT(current, lut, adjusted);
                current.Dispose();
                current = adjusted;
            }

            // 2. Temperature & Tint (RGB color balance shift)
            if (Math.Abs(adjustments.Temperature) > 1e-4 || Math.Abs(adjustments.Tint) > 1e-4)
            {
                current = ImageProcessingUtility.ColorBalance(current, adjustments.Temperature, adjustments.Tint);
            }

            // 3. Saturation and Hue shift (in HSV space)
            if (Math.Abs(adjustments.Saturation - 1.0) > 1e-4 || Math.Abs(adjustments.HueShift) > 1e-4)
            {
                if (Math.Abs(adjustments.HueShift) <= 1e-4)
                {
                    // Saturation-only case reuses the shared HSV helper.
                    var saturated = ImageProcessingUtility.AdjustSaturationByMultiplier(current, adjustments.Saturation);
                    current.Dispose();
                    current = saturated;
                }
                else
                {
                    using var hsv = new Mat();
                    Cv2.CvtColor(current, hsv, ColorConversionCodes.BGR2HSV);
                    var channels = Cv2.Split(hsv);
                    try
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

            // 5.5 Denoise (bilateral filter).
            if (adjustments.Denoise > 1e-4)
            {
                var denoised = new Mat();
                Cv2.BilateralFilter(current, denoised, 5, adjustments.Denoise * 100.0, adjustments.Denoise * 100.0);
                current.Dispose();
                current = denoised;
            }

            // 5.6 Vibrance: boost muted colors more than already-saturated ones.
            if (Math.Abs(adjustments.Vibrance) > 1e-4)
            {
                using var hsv = new Mat();
                Cv2.CvtColor(current, hsv, ColorConversionCodes.BGR2HSV);
                var channels = Cv2.Split(hsv);
                try
                {
                double v = adjustments.Vibrance;
                using var satLut = ImageProcessingUtility.BuildLut(s =>
                {
                    double t = s / 255.0;
                    double k = v >= 0 ? 1.0 + v * (1.0 - t) : 1.0 + v;
                    return 255.0 * t * k;
                });
                    Cv2.LUT(channels[1], satLut, channels[1]);
                    Cv2.Merge(channels, hsv);
                    var vibrant = new Mat();
                    Cv2.CvtColor(hsv, vibrant, ColorConversionCodes.HSV2BGR);
                    current.Dispose();
                    current = vibrant;
                }
                finally
                {
                    foreach (var ch in channels) ch.Dispose();
                }
            }

            // 5.7 Clarity: local contrast via CLAHE on the Luminance channel, blended with the original.
            if (adjustments.Clarity > 1e-4)
            {
                using var clarified = ImageProcessingUtility.ApplyClahe(current);
                var blended = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.Clarity, clarified, adjustments.Clarity, 0, blended);
                current.Dispose();
                current = blended;
            }

            // 5.8 Fade: lift blacks toward mid-gray for a matte film look.
            if (adjustments.Fade > 1e-4)
            {
                using var lut = ImageProcessingUtility.BuildLut(i => i + adjustments.Fade * (128.0 - i));
                var faded = new Mat();
                Cv2.LUT(current, lut, faded);
                current.Dispose();
                current = faded;
            }

            // 5.9 Monochrome: blend toward a grayscale rendition.
            if (adjustments.Monochrome > 1e-4)
            {
                using var grayBgr = current.ToGrayBgr();
                var mono = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.Monochrome, grayBgr, adjustments.Monochrome, 0, mono);
                current.Dispose();
                current = mono;
            }

            // 5.95 Grain: additive Gaussian noise for a film-like texture.
            if (adjustments.Grain > 1e-4)
            {
                using var noise = new Mat(current.Size(), MatType.CV_32FC3);
                Cv2.Randn(noise, Scalar.All(0), Scalar.All(30.0 * adjustments.Grain));
                using var currentF = new Mat();
                current.ConvertTo(currentF, MatType.CV_32FC3);
                using var noisyF = new Mat();
                Cv2.Add(currentF, noise, noisyF);
                var grained = new Mat();
                noisyF.ConvertTo(grained, MatType.CV_8UC3);
                current.Dispose();
                current = grained;
            }

            // 5.96 Dehaze: local contrast equalization plus a slight saturation lift.
            if (adjustments.Dehaze > 1e-4)
            {
                using var enhanced = ImageProcessingUtility.ApplyClahe(current);
                using var hsv = new Mat();
                Cv2.CvtColor(enhanced, hsv, ColorConversionCodes.BGR2HSV);
                var sat = Cv2.Split(hsv);
                try
                {
                    sat[1].ConvertTo(sat[1], MatType.CV_8UC1, 1.15);
                    Cv2.Merge(sat, hsv);
                    Cv2.CvtColor(hsv, enhanced, ColorConversionCodes.HSV2BGR);
                }
                finally
                {
                    foreach (var ch in sat) ch.Dispose();
                }

                var dehazed = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.Dehaze, enhanced, adjustments.Dehaze, 0, dehazed);
                current.Dispose();
                current = dehazed;
            }

            // 5.97 Soften: edge-preserving bilateral smoothing.
            if (adjustments.Soften > 1e-4)
            {
                using var softened = new Mat();
                Cv2.BilateralFilter(current, softened, 5, adjustments.Soften * 120.0, adjustments.Soften * 60.0);
                var blended = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.Soften, softened, adjustments.Soften, 0, blended);
                current.Dispose();
                current = blended;
            }

            // 5.98 Sepia tone blend.
            if (adjustments.SepiaTone > 1e-4)
            {
                using var sepia = ImageProcessingUtility.ApplySepia(current);
                var blended = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.SepiaTone, sepia, adjustments.SepiaTone, 0, blended);
                current.Dispose();
                current = blended;
            }

            // 5.99 Invert blend.
            if (adjustments.InvertAmount > 1e-4)
            {
                using var inverted = new Mat();
                Cv2.BitwiseNot(current, inverted);
                var blended = new Mat();
                Cv2.AddWeighted(current, 1.0 - adjustments.InvertAmount, inverted, adjustments.InvertAmount, 0, blended);
                current.Dispose();
                current = blended;
            }

            // 5.995 Posterize.
            if (adjustments.PosterizeLevels > 0)
            {
                using var lut = ImageProcessingUtility.BuildPosterizeLut(adjustments.PosterizeLevels);
                var posterized = new Mat();
                Cv2.LUT(current, lut, posterized);
                current.Dispose();
                current = posterized;
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

    /// <summary>Applies gray-world white balance followed by CLAHE contrast equalization.</summary>
    private static Mat ApplyAutoEnhance(Mat src)
    {
        var means = Cv2.Mean(src);
        double avg = (means.Val0 + means.Val1 + means.Val2) / 3.0;
        double bGain = avg / Math.Max(means.Val0, 1.0);
        double gGain = avg / Math.Max(means.Val1, 1.0);
        double rGain = avg / Math.Max(means.Val2, 1.0);

        var channels = Cv2.Split(src);
        Mat balanced;
        try
        {
            channels[0].ConvertTo(channels[0], MatType.CV_8UC1, bGain);
            channels[1].ConvertTo(channels[1], MatType.CV_8UC1, gGain);
            channels[2].ConvertTo(channels[2], MatType.CV_8UC1, rGain);
            balanced = new Mat();
            Cv2.Merge(channels, balanced);
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }

        using (balanced)
        {
            return ImageProcessingUtility.ApplyClahe(balanced);
        }
    }
}

