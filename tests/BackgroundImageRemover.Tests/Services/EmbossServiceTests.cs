using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class EmbossServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(8, 10, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = EmbossService.Apply(input, 135, 1.0, true);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_WithEdge_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(220, 220, 220), -1);

        using var result = EmbossService.Apply(input, 135, 1.0, true);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DifferentAngles_ChangeResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(220, 220, 220), -1);

        using var east = EmbossService.Apply(input, 0, 1.0, true);
        using var north = EmbossService.Apply(input, 90, 1.0, true);

        using var diff = new Mat();
        Cv2.Absdiff(east, north, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_GrayscaleVersusColor_ChangeResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(220, 220, 220), -1);

        using var gray = EmbossService.Apply(input, 135, 1.0, true);
        using var color = EmbossService.Apply(input, 135, 1.0, false);

        using var diff = new Mat();
        Cv2.Absdiff(gray, color, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_AngleSnapsTo45Degrees()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(40, 90, 140));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(220, 220, 220), -1);

        // 44.9° and 45° both snap to the 45° kernel, so the results must be identical.
        using var a = EmbossService.Apply(input, 44.9, 1.0, true);
        using var b = EmbossService.Apply(input, 45.0, 1.0, true);

        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }
}
