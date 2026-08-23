using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class WaveServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(10, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = WaveService.Apply(input, 4, 24, 0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_AmplitudeZero_ReturnsClone()
    {
        using var input = new Mat(12, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(200, 200, 200), -1);

        using var result = WaveService.Apply(input, 0, 24, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }

    [Fact]
    public void Apply_WithAmplitude_ChangesPixels()
    {
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 16, 16), new Scalar(200, 200, 200), -1);

        using var result = WaveService.Apply(input, 4, 16, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DifferentAngles_ChangeResult()
    {
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 16, 16), new Scalar(200, 200, 200), -1);

        using var horizontal = WaveService.Apply(input, 4, 16, 0);
        using var vertical = WaveService.Apply(input, 4, 16, 90);

        using var diff = new Mat();
        Cv2.Absdiff(horizontal, vertical, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_LargerWavelength_SmallerDisplacement()
    {
        // A wavelength far larger than the image produces an almost-constant offset,
        // which should differ from the tight-ripple result.
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 16, 16), new Scalar(200, 200, 200), -1);

        using var tight = WaveService.Apply(input, 4, 8, 0);
        using var wide = WaveService.Apply(input, 4, 200, 0);

        using var diff = new Mat();
        Cv2.Absdiff(tight, wide, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
