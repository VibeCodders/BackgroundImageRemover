using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class GrayscaleServiceTests : ServiceTestBase
{
    [Fact]
    public void ToGrayscale_StrengthZero_ReturnsUnchangedImage()
    {
        using var input = CreateTestInput(1, 1, new Scalar(10, 20, 30));
        AssertServiceNoChange(i => GrayscaleService.ToGrayscale(i, 0), input);
    }

    [Fact]
    public void ToGrayscale_StrengthOne_ConvertsToGrayscale()
    {
        using var input = CreateTestInput(1, 1, new Scalar(10, 20, 30));
        using var result = GrayscaleService.ToGrayscale(input, 1);

        var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        using var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);

        var expected = grayBgr.Get<Vec3b>(0, 0);
        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(expected, pixel);
        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void ToGrayscale_StrengthHalf_BlendsCorrectly()
    {
        using var input = CreateTestInput(1, 1, new Scalar(10, 20, 30));
        using var result = GrayscaleService.ToGrayscale(input, 0.5);

        var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        using var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);

        var expected = grayBgr.Get<Vec3b>(0, 0);
        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal((byte)((10 + expected[0]) / 2), pixel[0]);
        Assert.Equal((byte)((20 + expected[1]) / 2), pixel[1]);
        Assert.Equal((byte)((30 + expected[2]) / 2), pixel[2]);
        AssertPreservesSizeAndType(input, result);
    }
}
