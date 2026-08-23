using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class NoiseServiceTests
{
    [Fact]
    public void AddGaussianNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddGaussianNoise(input, 0);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void AddGaussianNoise_StrengthPositive_ModifiesImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddGaussianNoise(input, 0.5);

        ServiceTestHelper.AssertChangesPixels(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddSaltPepperNoise(input, 0);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthPositive_ModifiesImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = NoiseService.AddSaltPepperNoise(input, 0.5);

        ServiceTestHelper.AssertChangesPixels(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }
}
