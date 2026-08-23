using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class BokehServiceTests
{
    [Fact]
    public void Apply_ZeroCount_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 10, 0, 0.9, 4);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_ZeroOpacity_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 10, 50, 0, 4);

        ServiceTestHelper.AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_WithCircles_ChangesPixels()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 80, 1.0, 4);

        ServiceTestHelper.AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_DifferentBlur_ChangesResult()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var sharp = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 60, 1.0, 0);
        using var soft = BokehService.Apply(input, new Vec3b(255, 255, 255), 12, 60, 1.0, 12);

        ServiceTestHelper.AssertChangesPixels(sharp, soft);
    }
}
