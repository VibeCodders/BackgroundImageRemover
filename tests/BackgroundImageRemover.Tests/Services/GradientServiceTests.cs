using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class GradientServiceTests
{
    [Fact]
    public void Apply_ZeroOpacity_ReturnsUnchangedImage()
    {
        using var input = new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 90, 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_Linear_PreservesSizeAndType()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 90, 1);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_Radial_PreservesSizeAndType()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Radial,
            new Vec3b(255, 0, 0), new Vec3b(0, 0, 255), 0, 1);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_FullOpacity_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = GradientService.Apply(input, GradientKind.Linear,
            new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), 90, 1);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
