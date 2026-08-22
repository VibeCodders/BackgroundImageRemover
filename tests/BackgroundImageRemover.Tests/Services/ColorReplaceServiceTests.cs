using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class ColorReplaceServiceTests
{
    [Fact]
    public void Apply_ZeroTolerance_ReturnsUnchangedImage()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = ColorReplaceService.Apply(input,
            new Vec3b(10, 20, 30), new Vec3b(200, 100, 50), 0, 0.5, true);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
        Assert.Equal(input.Size(), result.Size());
    }

    [Fact]
    public void Apply_HighTolerance_ReplacesMatchingPixels()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(30, 200, 90)); // greenish
        using var result = ColorReplaceService.Apply(input,
            new Vec3b(30, 200, 90), new Vec3b(200, 100, 50), 1, 0, false);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(200, 100, 50), pixel);
    }

    [Fact]
    public void Apply_OutOfRange_PreservesUnchangedPixels()
    {
        // Target is blue; source pixel is red - well outside tolerance.
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 255)); // red
        using var result = ColorReplaceService.Apply(input,
            new Vec3b(255, 0, 0), new Vec3b(200, 100, 50), 0.05, 0, false);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(0, 0, 255), pixel);
    }

    [Fact]
    public void Apply_PreserveLuminance_KeepsPixelValue()
    {
        // A white pixel (highest luminance) replaced with black should stay light when
        // preserving luminance.
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(255, 255, 255));
        using var result = ColorReplaceService.Apply(input,
            new Vec3b(255, 255, 255), new Vec3b(20, 20, 20), 0.2, 0, true);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.True(pixel[0] > 200 && pixel[1] > 200 && pixel[2] > 200);
    }
}
