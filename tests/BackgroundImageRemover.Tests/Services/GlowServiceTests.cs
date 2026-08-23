using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class GlowServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(10, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = GlowService.Apply(input, 128, 3, 0.8);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_StrengthZero_ReturnsClone()
    {
        using var input = new Mat(12, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(6, 6), 3, new Scalar(255, 255, 255), -1);

        using var result = GlowService.Apply(input, 128, 3, 0);

        ServiceTestHelper.AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_BrightArea_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(8, 8), 3, new Scalar(255, 255, 255), -1);

        using var result = GlowService.Apply(input, 128, 3, 0.8);

        ServiceTestHelper.AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_NoBrightPixels_ReturnsClone()
    {
        using var input = new Mat(12, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = GlowService.Apply(input, 200, 3, 0.8);

        ServiceTestHelper.AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_HigherStrength_IncreasesEffect()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(8, 8), 3, new Scalar(255, 255, 255), -1);

        using var weak = GlowService.Apply(input, 128, 3, 0.2);
        using var strong = GlowService.Apply(input, 128, 3, 1.5);

        ServiceTestHelper.AssertChangesPixels(weak, strong);
    }
}
