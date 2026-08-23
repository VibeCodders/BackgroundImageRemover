using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class SketchServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(8, 10, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = SketchService.Apply(input, 7, false);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_UniformImage_ProducesUniformLightResult()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var result = SketchService.Apply(input, 7, false);

        // On a perfectly uniform image gray == blurred gray, so divide() saturates to white.
        using var gray = new Mat();
        Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
        var pixel = gray.Get<byte>(4, 4);
        Assert.Equal(255, pixel);
    }

    [Fact]
    public void Apply_Invert_FlipsTheSketch()
    {
        // A horizontal ramp gives a visible sketch gradient; inverting must change the pixels.
        using var input = new Mat(1, 32, MatType.CV_8UC3);
        for (int x = 0; x < 32; x++)
        {
            input.Set<Vec3b>(0, x, new Vec3b((byte)x, (byte)x, (byte)x));
        }

        using var normal = SketchService.Apply(input, 7, false);
        using var inverted = SketchService.Apply(input, 7, true);

        using var diff = new Mat();
        Cv2.Absdiff(normal, inverted, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DifferentBlur_ChangesResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(20, 60, 120));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(200, 200, 200), -1);

        using var soft = SketchService.Apply(input, 3, false);
        using var hard = SketchService.Apply(input, 25, false);

        using var diff = new Mat();
        Cv2.Absdiff(soft, hard, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
