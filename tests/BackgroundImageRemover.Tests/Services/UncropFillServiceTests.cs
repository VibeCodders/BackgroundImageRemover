using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Outpaint;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class UncropFillServiceTests
{
    private static Mat MakeUniformImage(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    [Fact]
    public void ExpandCanvas_MaskMarksOnlyNewArea()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(20, 10, Scalar.All(100));
        var padding = new CanvasPadding(5, 3, 5, 3);

        using var expanded = service.ExpandCanvas(source, padding, out var mask);
        using (mask)
        {
            Assert.Equal(30, expanded.Width);
            Assert.Equal(16, expanded.Height);

            using var innerRoi = new Mat(mask, new Rect(padding.Left, padding.Top, source.Width, source.Height));
            Assert.Equal(0, Cv2.CountNonZero(innerRoi));

            int totalOn = Cv2.CountNonZero(mask);
            int expectedOn = expanded.Width * expanded.Height - source.Width * source.Height;
            Assert.Equal(expectedOn, totalOn);
        }
    }

    [Fact]
    public void FillMirror_ReturnsExpectedSize_ForGivenPadding()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, Scalar.All(50));
        var padding = new CanvasPadding(10, 5, 8, 12);

        using var result = service.FillMirror(source, padding);

        Assert.Equal(source.Width + padding.Left + padding.Right, result.Width);
        Assert.Equal(source.Height + padding.Top + padding.Bottom, result.Height);
    }

    [Fact]
    public void FillMirror_PreservesOriginalPixelsInCenter()
    {
        var service = new UncropFillService();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(source, new Rect(5, 5, 20, 15), new Scalar(10, 200, 30), thickness: -1);
        var padding = new CanvasPadding(10, 10, 10, 10);

        using var result = service.FillMirror(source, padding);
        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));

        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Theory]
    [InlineData(UncropInpaintMethod.Telea)]
    [InlineData(UncropInpaintMethod.NavierStokes)]
    public void FillInpaint_ReturnsExpectedSize_ForBothMethods(UncropInpaintMethod method)
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, Scalar.All(120));
        var padding = new CanvasPadding(6, 4, 6, 4);

        using var result = service.FillInpaint(source, padding, method);

        Assert.Equal(source.Width + padding.Left + padding.Right, result.Width);
        Assert.Equal(source.Height + padding.Top + padding.Bottom, result.Height);
    }

    [Fact]
    public void FillSolidColor_FillsBorderWithSampledColor()
    {
        var service = new UncropFillService();
        var color = new Scalar(60, 90, 120);
        using var source = MakeUniformImage(50, 40, color);
        var padding = new CanvasPadding(6, 6, 6, 6);

        using var result = service.FillSolidColor(source, padding, blurred: false);
        using var cornerRoi = new Mat(result, new Rect(0, 0, padding.Left, padding.Top));
        var mean = Cv2.Mean(cornerRoi);

        Assert.InRange(mean.Val0, color.Val0 - 1, color.Val0 + 1);
        Assert.InRange(mean.Val1, color.Val1 - 1, color.Val1 + 1);
        Assert.InRange(mean.Val2, color.Val2 - 1, color.Val2 + 1);
    }

    [Fact]
    public void FillSolidColor_Blurred_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(source, new Rect(5, 5, 20, 15), new Scalar(10, 200, 30), thickness: -1);
        var padding = new CanvasPadding(8, 8, 8, 8);

        using var result = service.FillSolidColor(source, padding, blurred: true);

        Assert.Equal(source.Width + padding.Left + padding.Right, result.Width);
        Assert.Equal(source.Height + padding.Top + padding.Bottom, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    private static bool MatsAreEqual(Mat a, Mat b)
    {
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        var sum = Cv2.Sum(diff);
        return sum.Val0 == 0 && sum.Val1 == 0 && sum.Val2 == 0;
    }
}
