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
}

