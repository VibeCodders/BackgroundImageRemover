using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class NoiseServiceTests
{
    [Fact]
    public void AddGaussianNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddGaussianNoise(input, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(gray));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void AddGaussianNoise_StrengthPositive_ModifiesImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddGaussianNoise(input, 0.5);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddSaltPepperNoise(input, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(gray));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthPositive_ModifiesImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddSaltPepperNoise(input, 0.5);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
