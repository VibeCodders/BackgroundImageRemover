using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class GradientServiceTests
{
    [Fact]
    public void Apply_ZeroOpacity_ReturnsUnchangedImage()
    {
        using var input = new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 90, 0);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Linear_PreservesSizeAndType()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 90, 1);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Radial_PreservesSizeAndType()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Radial,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 0, 1);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_FullOpacity_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), 90, 1);

        ServiceTestHelper.AssertChangesPixels(input, result);
    }
}
