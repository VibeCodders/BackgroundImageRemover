using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the color sampler used by the Color Replace / Color Picker tools.</summary>
public class ColorPickerServiceTests
{
    [Fact]
    public void Sample_ReturnsPixelAtCoordinate()
    {
        using var image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));

        Assert.Equal(new Vec3b(10, 20, 30), ColorPickerService.Sample(image, 5, 5));
    }

    [Fact]
    public void Sample_ClampsToImageBounds()
    {
        using var image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using (var roi = new Mat(image, new Rect(0, 0, 1, 1))) roi.SetTo(new Scalar(1, 2, 3));
        using (var roi = new Mat(image, new Rect(9, 9, 1, 1))) roi.SetTo(new Scalar(7, 8, 9));

        Assert.Equal(new Vec3b(1, 2, 3), ColorPickerService.Sample(image, -50, -50));
        Assert.Equal(new Vec3b(7, 8, 9), ColorPickerService.Sample(image, 999, 999));
    }

    [Fact]
    public void SampleAverage_AveragesTheNeighborhood()
    {
        using var image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0, 0, 0));
        // Left half red, right half blue: the average at the border is a purple-ish mix.
        using (var roi = new Mat(image, new Rect(0, 0, 5, 10))) roi.SetTo(new Scalar(0, 0, 255));
        using (var roi = new Mat(image, new Rect(5, 0, 5, 10))) roi.SetTo(new Scalar(255, 0, 0));

        var avg = ColorPickerService.SampleAverage(image, 5, 5, radius: 2);

        // Window x in [3,7]: 2 red columns and 3 blue columns (the sample point sits on the
        // blue side). B = 15/25 * 255 = 153, R = 10/25 * 255 = 102.
        Assert.Equal(153, avg.Item0);
        Assert.Equal(0, avg.Item1);
        Assert.Equal(102, avg.Item2);
    }

    [Fact]
    public void SampleAverage_ClampsRegionToImage()
    {
        using var image = new Mat(10, 10, MatType.CV_8UC3, new Scalar(50, 50, 50));

        // Sampling near the corner with a huge radius must not throw and returns the corner pixel.
        Assert.Equal(new Vec3b(50, 50, 50), ColorPickerService.SampleAverage(image, 0, 0, 1000));
    }

    [Fact]
    public void Sample_EmptyImage_ReturnsDefaultWithoutThrowing()
    {
        using var empty = new Mat(0, 0, MatType.CV_8UC3);

        Assert.Equal(new Vec3b(0, 0, 0), ColorPickerService.Sample(empty, 5, 5));
        Assert.Equal(new Vec3b(0, 0, 0), ColorPickerService.SampleAverage(empty, 5, 5, 3));
    }

    [Fact]
    public void ToHex_FormatsBgrAsRgbHex()
    {
        // BGR (0, 255, 128) -> RGB "#80FF00"
        Assert.Equal("#80FF00", ColorPickerService.ToHex(new Vec3b(0, 255, 128)));
    }

    [Fact]
    public void ToHsv_RedBecomesHueZero()
    {
        var (h, s, v) = ColorPickerService.ToHsv(new Vec3b(0, 0, 255)); // pure red in BGR

        Assert.Equal(0.0, h, 1);
        Assert.Equal(100.0, s, 1);
        Assert.Equal(100.0, v, 1);
    }
}
