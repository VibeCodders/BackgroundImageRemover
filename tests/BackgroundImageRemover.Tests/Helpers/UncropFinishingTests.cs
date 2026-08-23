using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Helpers;

public class UncropFinishingTests
{
    [Fact]
    public void AddGrain_ProducesSameSizeAndChangesPixels()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = UncropOperationHelper.AddGrain(src, 0.5);

        Assert.Equal(src.Size(), result.Size());
        ServiceTestHelper.AssertChangesPixels(src, result);
    }

    [Fact]
    public void ApplyFinishing_AppliesFlipAndBorder()
    {
        using var src = new Mat(1, 2, MatType.CV_8UC3);
        src.Set(0, 0, new Vec3b(0, 0, 255));    // left red
        src.Set(0, 1, new Vec3b(255, 0, 0));    // right blue

        var config = new UncropOperationHelper.UncropConfig
        {
            FlipHorizontal = true,
            BorderThickness = 2,
            BorderColor = System.Windows.Media.Color.FromRgb(255, 255, 255)
        };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(6, result.Width);  // 2 + 2*2
        Assert.Equal(5, result.Height); // 1 + 2*2

        // After the flip, the left pixel is blue and the right pixel is red.
        var left = result.At<Vec4b>(2, 2);
        Assert.Equal(255, left.Item0);
        Assert.Equal(0, left.Item2);

        var right = result.At<Vec4b>(2, 3);
        Assert.Equal(0, right.Item0);
        Assert.Equal(255, right.Item2);

        // The border is the configured color.
        var corner = result.At<Vec4b>(0, 0);
        Assert.Equal(255, corner.Item0);
        Assert.Equal(255, corner.Item1);
        Assert.Equal(255, corner.Item2);
        Assert.Equal(255, corner.Item3);
    }

    [Fact]
    public void ApplyFinishing_RoundsCorners()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));

        var config = new UncropOperationHelper.UncropConfig { CornerRadius = 4 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(0, result.At<Vec4b>(0, 0).Item3); // corner transparent
        Assert.Equal(255, result.At<Vec4b>(5, 5).Item3); // center opaque
    }

    [Fact]
    public void SizePreset_ComputesCenteredPadding()
    {
        var options = new UncropOptionsViewModel
        {
            ImageSizeProvider = () => new Size(500, 500)
        };

        options.SelectedSizePreset = new UncropSizePreset("Test", new Size(1024, 1024));

        Assert.Equal(new CanvasPadding(262, 262, 262, 262), options.Padding);
    }

    [Fact]
    public void ApplyFinishing_Rotate_SwapsDimensions()
    {
        using var src = new Mat(1, 2, MatType.CV_8UC3, new Scalar(0, 0, 255));

        var config = new UncropOperationHelper.UncropConfig { RotateAngle = 90 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(1, result.Width);
        Assert.Equal(2, result.Height);
    }

    [Fact]
    public void ApplyFinishing_Vignette_DarkensCorners()
    {
        using var src = new Mat(40, 40, MatType.CV_8UC3, new Scalar(200, 200, 200));

        var config = new UncropOperationHelper.UncropConfig { Vignette = 0.9 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.True(result.At<Vec4b>(20, 20).Item0 > result.At<Vec4b>(0, 0).Item0);
    }

    [Fact]
    public void ApplyFinishing_SaturationZero_ProducesGray()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red

        var config = new UncropOperationHelper.UncropConfig { Saturation = 0.0 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);
        var px = result.At<Vec4b>(2, 2);

        Assert.True(Math.Abs(px.Item0 - px.Item2) <= 1); // B == R now
    }

    [Fact]
    public void ApplyFinishing_Contrast_DoublesValue()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        var config = new UncropOperationHelper.UncropConfig { Contrast = 2.0 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(200, result.At<Vec4b>(2, 2).Item0);
    }

    [Fact]
    public void ApplyFinishing_Brightness_ShiftsValue()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        var config = new UncropOperationHelper.UncropConfig { Brightness = 30 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(130, result.At<Vec4b>(2, 2).Item0);
    }

    [Fact]
    public void ApplyFinishing_Temperature_WarmsTheImage()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        var config = new UncropOperationHelper.UncropConfig { Temperature = 40 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);
        var px = result.At<Vec4b>(2, 2);

        Assert.True(px.Item2 > 100); // red boosted
        Assert.True(px.Item0 < 100); // blue reduced
    }

    [Fact]
    public void ApplyFinishing_Denoise_PreservesSize()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        var config = new UncropOperationHelper.UncropConfig { Denoise = 0.5 };

        using var result = UncropOperationHelper.ApplyFinishing(src, config);

        Assert.Equal(src.Size(), new Size(result.Width, result.Height));
    }
}
