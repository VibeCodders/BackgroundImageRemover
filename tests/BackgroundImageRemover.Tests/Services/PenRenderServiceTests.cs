using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class PenRenderServiceTests
{
    [Fact]
    public void Draw_NullOrEmptyStrokes_ReturnsUnchangedImage()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var empty = PenRenderService.Draw(input, new List<PenStroke>(), new Vec3b(0, 0, 0));
        AssertMatches(input, empty);

        using var nullResult = PenRenderService.Draw(input, null, new Vec3b(0, 0, 0));
        AssertMatches(input, nullResult);
    }

    [Fact]
    public void Draw_WithStroke_ChangesPixels()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var stroke = new PenStroke(new List<Point>
        {
            new(4, 16), new(10, 16), new(16, 16), new(22, 16)
        }, RadiusPx: 3);

        using var result = PenRenderService.Draw(input, new List<PenStroke> { stroke }, new Vec3b(255, 255, 255));

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Draw_SingleClickPaintsDot()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var stroke = new PenStroke(new List<Point> { new(8, 8) }, RadiusPx: 2);

        using var result = PenRenderService.Draw(input, new List<PenStroke> { stroke }, new Vec3b(255, 0, 0));

        var pixel = result.Get<Vec3b>(8, 8);
        Assert.Equal(new Vec3b(255, 0, 0), pixel);
    }

    private static void AssertMatches(Mat a, Mat b)
    {
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }
}
