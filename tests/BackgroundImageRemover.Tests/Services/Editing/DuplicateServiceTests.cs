using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services.Editing;

public class DuplicateServiceTests
{
    [Fact]
    public void Duplicate_ReturnsCloneWithSameSizeAndType()
    {
        using var input = new Mat(4, 6, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = DuplicateService.Duplicate(input);

        Assert.False(result.Empty());
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Duplicate_ReturnsIndependentData()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(0, 0, 0));
        input.Set(0, 0, new Vec3b(255, 0, 0));

        using var result = DuplicateService.Duplicate(input);
        // Mutate the source after cloning: the clone must be unaffected.
        input.Set(0, 0, new Vec3b(0, 255, 0));

        var px = result.Get<Vec3b>(0, 0);
        Assert.Equal(255, px.Item0);
        Assert.Equal(0, px.Item1);
    }

    [Fact]
    public void Duplicate_NullInput_ReturnsEmptyMat()
    {
        using var result = DuplicateService.Duplicate(null!);

        Assert.True(result.Empty());
    }

    [Fact]
    public void Duplicate_EmptyInput_ReturnsEmptyMat()
    {
        using var empty = new Mat();

        using var result = DuplicateService.Duplicate(empty);

        Assert.True(result.Empty());
    }

    [Fact]
    public void Duplicate_PreservesAlphaChannel()
    {
        using var bgr = new Mat(3, 3, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var alpha = new Mat(3, 3, MatType.CV_8UC1, new Scalar(128));
        using var bgra = bgr.ToBgra(alpha);

        using var result = DuplicateService.Duplicate(bgra);

        Assert.Equal(4, result.Channels());
        Assert.Equal(bgra.Size(), result.Size());
    }
}
