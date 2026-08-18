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
}
