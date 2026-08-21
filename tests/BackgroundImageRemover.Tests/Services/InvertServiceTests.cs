using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class InvertServiceTests
{
    [Fact]
    public void Invert_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = InvertService.Invert(input, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        Assert.Equal(0, Cv2.CountNonZero(diff));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
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
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Invert_StrengthHalf_BlendsCorrectly()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = InvertService.Invert(input, 0.5);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal((byte)((10 + (255 - 10)) / 2), pixel[0]);
        Assert.Equal((byte)((20 + (255 - 20)) / 2), pixel[1]);
        Assert.Equal((byte)((30 + (255 - 30)) / 2), pixel[2]);
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
