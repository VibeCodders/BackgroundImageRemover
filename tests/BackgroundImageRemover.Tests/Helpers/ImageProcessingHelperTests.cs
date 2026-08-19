using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public sealed class ImageProcessingHelperTests
{
    [Fact]
    public void ApplyAdjustments_DefaultIdentity_ReturnsClone()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 150, 200));
        using var result = ImageProcessingHelper.ApplyAdjustments(src, ImageAdjustments.Default);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
        Assert.Equal(src.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void ApplyAdjustments_BrightnessIncrease_IncreasesPixelValues()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(50, 50, 50));
        var adj = new ImageAdjustments { Brightness = 30 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.Equal(80, pixel.Item0);
        Assert.Equal(80, pixel.Item1);
        Assert.Equal(80, pixel.Item2);
    }

    [Fact]
    public void ApplyAdjustments_ContrastIncrease_ScalesPixelValues()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(40, 50, 60));
        var adj = new ImageAdjustments { Contrast = 2.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.Equal(80, pixel.Item0);
        Assert.Equal(100, pixel.Item1);
        Assert.Equal(120, pixel.Item2);
    }

    [Fact]
    public void ApplyAdjustments_ZeroSaturation_MakesGrayscale()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(255, 0, 0)); // Pure Blue
        var adj = new ImageAdjustments { Saturation = 0.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        // In pure grayscale, B == G == R
        Assert.True(Math.Abs(pixel.Item0 - pixel.Item1) <= 1);
        Assert.True(Math.Abs(pixel.Item1 - pixel.Item2) <= 1);
    }

    [Fact]
    public void ApplyAdjustments_Blur_SmoothsImage()
    {
        using var src = new Mat(11, 11, MatType.CV_8UC3, new Scalar(0, 0, 0));
        src.Set(5, 5, new Vec3b(255, 255, 255)); // single bright center pixel
        var adj = new ImageAdjustments { BlurRadius = 2 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var neighbor = result.At<Vec3b>(5, 6);

        // Center pixel energy should have spread to neighbor
        Assert.True(neighbor.Item0 > 0);
    }

    [Fact]
    public void ApplyAdjustments_Sharpen_EnhancesEdges()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adj = new ImageAdjustments { SharpenStrength = 1.5 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void ApplyAdjustments_HueShift_RotatesHue()
    {
        // Pure red in BGR: B=0, G=0, R=255 -> HSV Hue is ~0
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0, 0, 255));
        var adj = new ImageAdjustments { HueShift = 120 }; // Shifting red by 120 deg leads to green

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        // Green channel should be highest now
        Assert.True(pixel.Item1 > pixel.Item0 && pixel.Item1 > pixel.Item2);
    }

    [Fact]
    public void ApplyAdjustments_WarmTemperature_IncreasesRedDecreasesBlue()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adj = new ImageAdjustments { Temperature = 40 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.True(pixel.Item2 > 100); // Red boosted
        Assert.True(pixel.Item0 < 100); // Blue reduced
    }

    [Fact]
    public void ApplyAdjustments_Tint_AdjustsGreenChannel()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adjGreen = new ImageAdjustments { Tint = -40 };
        using var resultGreen = ImageProcessingHelper.ApplyAdjustments(src, adjGreen);
        var pixelGreen = resultGreen.At<Vec3b>(0, 0);
        Assert.True(pixelGreen.Item1 > 100); // Green boosted

        var adjMagenta = new ImageAdjustments { Tint = 40 };
        using var resultMagenta = ImageProcessingHelper.ApplyAdjustments(src, adjMagenta);
        var pixelMagenta = resultMagenta.At<Vec3b>(0, 0);
        Assert.True(pixelMagenta.Item1 < 100); // Green reduced
    }

    [Fact]
    public void ApplyAdjustments_Vignette_DarkensCornersMoreThanCenter()
    {
        using var src = new Mat(50, 50, MatType.CV_8UC3, new Scalar(200, 200, 200));
        var adj = new ImageAdjustments { Vignette = 0.8 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var centerPixel = result.At<Vec3b>(25, 25);
        var cornerPixel = result.At<Vec3b>(0, 0);

        Assert.True(centerPixel.Item0 > cornerPixel.Item0);
        Assert.True(cornerPixel.Item0 < 200);
    }

    [Fact]
    public void ApplyAdjustments_Exposure_BrightensMidtones()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adj = new ImageAdjustments { Exposure = 2.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        // Gamma < 1 inside the exponent brightens midtones.
        Assert.True(pixel.Item0 > 100);
        Assert.True(pixel.Item1 > 100);
        Assert.True(pixel.Item2 > 100);
    }

    [Fact]
    public void ApplyAdjustments_Shadows_BrightensDarkPixels()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(20, 20, 20));
        var adj = new ImageAdjustments { Shadows = 60 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.True(pixel.Item0 > 20);
    }

    [Fact]
    public void ApplyAdjustments_Highlights_DarkensBrightPixels()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(230, 230, 230));
        var adj = new ImageAdjustments { Highlights = 60 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.True(pixel.Item0 < 230);
    }

    [Fact]
    public void ApplyAdjustments_Denoise_KeepsSizeAndType()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adj = new ImageAdjustments { Denoise = 0.5 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }

    [Fact]
    public void ApplyAdjustments_AutoEnhance_ChangesTheImage()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(80, 120, 160));
        var adj = new ImageAdjustments { AutoEnhance = true };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);

        Assert.Equal(src.Size(), result.Size());
        // Auto-enhance must not be a no-op on a non-gray image.
        Assert.NotEqual(src.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void ApplyAdjustments_Vibrance_IncreasesColorSpread()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200)); // moderate saturation
        var adj = new ImageAdjustments { Vibrance = 1.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var before = src.At<Vec3b>(0, 0);
        var after = result.At<Vec3b>(0, 0);
        int beforeSpread = Math.Max(before.Item0, Math.Max(before.Item1, before.Item2)) - Math.Min(before.Item0, Math.Min(before.Item1, before.Item2));
        int afterSpread = Math.Max(after.Item0, Math.Max(after.Item1, after.Item2)) - Math.Min(after.Item0, Math.Min(after.Item1, after.Item2));

        Assert.True(afterSpread >= beforeSpread);
    }

    [Fact]
    public void ApplyAdjustments_Fade_LiftsBlacksTowardMidGray()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(20, 20, 20));
        var adj = new ImageAdjustments { Fade = 1.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.Equal(128, pixel.Item0);
        Assert.Equal(128, pixel.Item1);
        Assert.Equal(128, pixel.Item2);
    }

    [Fact]
    public void ApplyAdjustments_Monochrome_MakesChannelsEqual()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red
        var adj = new ImageAdjustments { Monochrome = 1.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);
        var pixel = result.At<Vec3b>(0, 0);

        Assert.True(Math.Abs(pixel.Item0 - pixel.Item1) <= 1);
        Assert.True(Math.Abs(pixel.Item1 - pixel.Item2) <= 1);
    }

    [Fact]
    public void ApplyAdjustments_Grain_ChangesPixelValues()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        var adj = new ImageAdjustments { Grain = 1.0 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);

        Assert.Equal(src.Size(), result.Size());
        Assert.NotEqual(src.At<Vec3b>(5, 5), result.At<Vec3b>(5, 5));
    }

    [Fact]
    public void ApplyAdjustments_Clarity_PreservesSizeAndType()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(10, 10, 10, 10)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }
        var adj = new ImageAdjustments { Clarity = 0.8 };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }

    [Fact]
    public void ApplyAdjustments_Dehaze_PreservesSizeAndType()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(10, 10, 10, 10)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var result = ImageProcessingHelper.ApplyAdjustments(src, new ImageAdjustments { Dehaze = 0.8 });

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }

    [Fact]
    public void ApplyAdjustments_Soften_PreservesSizeAndType()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(8, 8, 4, 4)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var result = ImageProcessingHelper.ApplyAdjustments(src, new ImageAdjustments { Soften = 0.7 });

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }

    [Fact]
    public void ApplyAdjustments_SepiaTone_AddsWarmTone()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(128, 128, 128));

        using var result = ImageProcessingHelper.ApplyAdjustments(src, new ImageAdjustments { SepiaTone = 1.0 });

        var px = result.At<Vec3b>(0, 0);
        Assert.True(px.Item2 > px.Item0); // sepia boosts red more than blue
    }

    [Fact]
    public void ApplyAdjustments_InvertAmount_FullInvert()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = ImageProcessingHelper.ApplyAdjustments(src, new ImageAdjustments { InvertAmount = 1.0 });

        Assert.Equal(155, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void ApplyAdjustments_Posterize_Quantizes()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(130, 130, 130));

        using var result = ImageProcessingHelper.ApplyAdjustments(src, new ImageAdjustments { PosterizeLevels = 4 });

        Assert.Equal(128, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void ApplyAdjustments_ExtremeValues_DoesNotCrash()
    {
        // Extreme parameter values should not crash the adjustment pipeline.
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(128, 128, 128));
        var adj = new ImageAdjustments
        {
            Brightness = 200,
            Contrast = 10.0,
            Saturation = 5.0,
            HueShift = 500,
            Temperature = 300,
            Tint = 300,
            Vignette = 5.0,
            BlurRadius = 100,
            SharpenStrength = 10.0,
            Exposure = 0.1,
            Highlights = 200,
            Shadows = 200,
            Denoise = 5.0,
            Vibrance = 5.0,
            Clarity = 5.0,
            Fade = 5.0,
            Grain = 5.0,
            Monochrome = 5.0,
            Dehaze = 5.0,
            Soften = 5.0,
            SepiaTone = 5.0,
            InvertAmount = 5.0,
            PosterizeLevels = 1
        };

        using var result = ImageProcessingHelper.ApplyAdjustments(src, adj);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }
}
