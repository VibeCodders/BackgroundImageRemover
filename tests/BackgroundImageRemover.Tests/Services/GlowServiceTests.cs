using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class GlowServiceTests : ServiceTestBase
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = CreateTestInput(10, 12, new Scalar(40, 90, 140));
        using var result = GlowService.Apply(input, 128, 3, 0.8);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_StrengthZero_ReturnsClone()
    {
        using var input = CreateTestInput(12, 12, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(6, 6), 3, new Scalar(255, 255, 255), -1);

        using var result = GlowService.Apply(input, 128, 3, 0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_BrightArea_ChangesPixels()
    {
        using var input = CreateTestInput(16, 16, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(8, 8), 3, new Scalar(255, 255, 255), -1);

        using var result = GlowService.Apply(input, 128, 3, 0.8);

        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_NoBrightPixels_ReturnsClone()
    {
        using var input = CreateTestInput(12, 12, new Scalar(40, 90, 140));
        using var result = GlowService.Apply(input, 200, 3, 0.8);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_HigherStrength_IncreasesEffect()
    {
        using var input = CreateTestInput(16, 16, new Scalar(40, 90, 140));
        Cv2.Circle(input, new Point(8, 8), 3, new Scalar(255, 255, 255), -1);

        using var weak = GlowService.Apply(input, 128, 3, 0.2);
        using var strong = GlowService.Apply(input, 128, 3, 1.5);

        AssertChangesPixels(weak, strong);
    }
}
