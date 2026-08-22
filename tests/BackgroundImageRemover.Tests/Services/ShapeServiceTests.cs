using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class ShapeServiceTests
{
    [Fact]
    public void Apply_NoStrokeNoFill_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(4, 4, 16, 16);
        using var result = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(0, 0, 0), 0, false, new Vec3b(0, 0, 0), 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
        Assert.Equal(input.Size(), result.Size());
    }

    [Fact]
    public void Apply_RectangleStroke_DrawsOnImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(4, 4, 16, 16);
        using var result = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, false, new Vec3b(0, 0, 0), 0);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_EllipseAndLineAndArrow_ProduceOutput()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(4, 4, 16, 16);

        foreach (var kind in new[] { ShapeKind.Ellipse, ShapeKind.Line, ShapeKind.Arrow })
        {
            using var result = ShapeService.Apply(input, kind, rect,
                new Vec3b(255, 255, 255), 2, false, new Vec3b(0, 0, 0), 0);
            Assert.Equal(input.Size(), result.Size());
            Assert.Equal(input.Type(), result.Type());
        }
    }

    [Fact]
    public void Apply_Fill_ChangesInteriorPixels()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(6, 6, 8, 8);
        using var result = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
