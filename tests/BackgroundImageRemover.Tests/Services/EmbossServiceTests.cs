using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class EmbossServiceTests
{
    [Fact]
    public void Emboss_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = EmbossService.Emboss(input, 0);

        Assert.Equal(0, Cv2.Norm(input, result, NormTypes.L1));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Emboss_StrengthPositive_ModifiesImage()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = EmbossService.Emboss(input, 0.5);

        Assert.True(Cv2.Norm(input, result, NormTypes.L1) > 0);
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
