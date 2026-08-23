using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class InvertServiceTests
{
    [Fact]
    public void Invert_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = InvertService.Invert(input, 0);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Invert_StrengthOne_InvertsColorsCorrectly()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = InvertService.Invert(input, 1);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal((byte)(255 - 10), pixel[0]);
        Assert.Equal((byte)(255 - 20), pixel[1]);
        Assert.Equal((byte)(255 - 30), pixel[2]);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Invert_StrengthHalf_BlendsCorrectly()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = InvertService.Invert(input, 0.5);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal((byte)128, pixel[0]);
        Assert.Equal((byte)128, pixel[1]);
        Assert.Equal((byte)128, pixel[2]);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }
}
