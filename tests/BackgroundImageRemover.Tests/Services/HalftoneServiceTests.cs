using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class HalftoneServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_WithDarkArea_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_UniformWhite_ProducesBlankWhiteCanvas()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(255, 255, 255));
        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        // Nothing dark → no dots → the whole canvas stays white.
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(y, x));
            }
        }
    }

    [Fact]
    public void Apply_Invert_ChangesResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var normal = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);
        using var inverted = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), true);

        using var diff = new Mat();
        Cv2.Absdiff(normal, inverted, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DifferentDotColor_ChangesResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var black = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);
        using var red = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 220), false);

        using var diff = new Mat();
        Cv2.Absdiff(black, red, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
