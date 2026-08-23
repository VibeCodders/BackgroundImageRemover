using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class BokehServiceTests
{
    [Fact]
    public void Apply_ZeroCount_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 10, 0, 0.9, 4);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_ZeroOpacity_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 10, 50, 0, 4);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }

    [Fact]
    public void Apply_WithCircles_ChangesPixels()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 80, 1.0, 4);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DifferentBlur_ChangesResult()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var sharp = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 60, 1.0, 0);
        using var soft = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 60, 1.0, 12);

        using var diff = new Mat();
        Cv2.Absdiff(sharp, soft, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
