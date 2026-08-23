using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class OilPaintServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(10, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = OilPaintService.Apply(input, 3, 8);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_WithDetail_ChangesPixels()
    {
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(60, 100, 160));
        Cv2.Rectangle(input, new Rect(6, 6, 12, 12), new Scalar(220, 220, 220), -1);

        using var result = OilPaintService.Apply(input, 3, 8);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_LargerBrush_ChangesResult()
    {
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(60, 100, 160));
        Cv2.Rectangle(input, new Rect(6, 6, 12, 12), new Scalar(220, 220, 220), -1);

        using var small = OilPaintService.Apply(input, 1, 8);
        using var large = OilPaintService.Apply(input, 6, 8);

        using var diff = new Mat();
        Cv2.Absdiff(small, large, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_UniformImage_StaysUniform()
    {
        using var input = new Mat(12, 12, MatType.CV_8UC3, new Scalar(120, 90, 60));
        using var result = OilPaintService.Apply(input, 3, 8);

        Vec3b first = result.Get<Vec3b>(0, 0);
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                Assert.Equal(first, result.Get<Vec3b>(y, x));
            }
        }
    }

    [Fact]
    public void Apply_FewerDetailLevels_ChangesResult()
    {
        using var input = new Mat(24, 24, MatType.CV_8UC3, new Scalar(60, 100, 160));
        Cv2.Rectangle(input, new Rect(6, 6, 12, 12), new Scalar(220, 220, 220), -1);

        using var flat = OilPaintService.Apply(input, 3, 2);
        using var fine = OilPaintService.Apply(input, 3, 32);

        using var diff = new Mat();
        Cv2.Absdiff(flat, fine, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }
}
