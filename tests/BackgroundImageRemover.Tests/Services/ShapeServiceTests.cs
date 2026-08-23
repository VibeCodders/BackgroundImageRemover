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

    [Fact]
    public void Apply_PolygonAndStar_ProduceOutput()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(8, 8, 40, 40);

        foreach (var kind in new[] { ShapeKind.Polygon, ShapeKind.Star })
        {
            using var result = ShapeService.Apply(input, kind, rect,
                new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1,
                segments: 6, starRatio: 0.4);

            Assert.Equal(input.Size(), result.Size());
            Assert.Equal(input.Type(), result.Type());

            using var diff = new Mat();
            Cv2.Absdiff(input, result, diff);
            using var diffGray = new Mat();
            Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
            Assert.True(Cv2.CountNonZero(diffGray) > 0);
        }
    }

    [Fact]
    public void Apply_Polygon_NoStrokeNoFill_ReturnsUnchangedImage()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(4, 4, 16, 16);
        using var result = ShapeService.Apply(input, ShapeKind.Star, rect,
            new Vec3b(0, 0, 0), 0, false, new Vec3b(0, 0, 0), 0, segments: 5, starRatio: 0.4);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }

    [Fact]
    public void Apply_RotatedRectangle_DiffersFromAxisAligned()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(10, 10, 30, 20);

        using var flat = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 0);
        using var rotated = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 25);

        using var diff = new Mat();
        Cv2.Absdiff(flat, rotated, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_RotatedEllipse_DiffersFromAxisAligned()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(10, 10, 30, 20);

        using var flat = ShapeService.Apply(input, ShapeKind.Ellipse, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 0);
        using var rotated = ShapeService.Apply(input, ShapeKind.Ellipse, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 25);

        using var diff = new Mat();
        Cv2.Absdiff(flat, rotated, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_RotationZeroAndOneEighty_AreBothAxisAligned()
    {
        using var input = new Mat(64, 64, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var rect = new Rect(10, 10, 30, 20);

        using var flat = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 0);
        using var halfTurn = ShapeService.Apply(input, ShapeKind.Rectangle, rect,
            new Vec3b(255, 255, 255), 2, true, new Vec3b(200, 100, 50), 1, rotation: 180);

        using var diff = new Mat();
        Cv2.Absdiff(flat, halfTurn, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }
}
