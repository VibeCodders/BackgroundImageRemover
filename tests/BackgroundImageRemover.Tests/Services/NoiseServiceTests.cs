using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class NoiseServiceTests : ServiceTestBase
{
    [Fact]
    public void AddGaussianNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = CreateTestInput(100, 100, new Scalar(100, 100, 100));
        AssertServiceNoChange(i => NoiseService.AddGaussianNoise(i, 0), input);
    }

    [Fact]
    public void AddGaussianNoise_StrengthPositive_ModifiesImage()
    {
        using var input = CreateTestInput(100, 100, new Scalar(100, 100, 100));
        AssertServiceChangesPixels(i => NoiseService.AddGaussianNoise(i, 0.5), input);
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = CreateTestInput(100, 100, new Scalar(100, 100, 100));
        AssertServiceNoChange(i => NoiseService.AddSaltPepperNoise(i, 0), input);
    }

    [Fact]
    public void AddSaltPepperNoise_StrengthPositive_ModifiesImage()
    {
        using var input = CreateTestInput(100, 100, new Scalar(100, 100, 100));
        AssertServiceChangesPixels(i => NoiseService.AddSaltPepperNoise(i, 0.5), input);
    }
}
