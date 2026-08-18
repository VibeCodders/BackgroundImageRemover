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

    [Theory]
    [InlineData(UncropMirrorType.Reflect101)]
    [InlineData(UncropMirrorType.Reflect)]
    public void FillMirror_ReturnsExpectedSize_ForGivenPaddingAndType(UncropMirrorType mirrorType)
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, Scalar.All(50));
        var padding = new CanvasPadding(10, 5, 8, 12);

        using var result = service.FillMirror(source, padding, mirrorType);

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

        using var result = service.FillInpaint(source, padding, method, inpaintRadius: 8, blendMargin: 3);

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
    public void FillSolidColor_CustomColor_FillsWithSpecifiedScalar()
    {
        var service = new UncropFillService();
        var sourceColor = new Scalar(10, 20, 30);
        var customColor = new Scalar(200, 150, 100);
        using var source = MakeUniformImage(50, 40, sourceColor);
        var padding = new CanvasPadding(8, 8, 8, 8);

        using var result = service.FillSolidColor(source, padding, blurred: false, customColor: customColor);
        using var cornerRoi = new Mat(result, new Rect(0, 0, padding.Left, padding.Top));
        var mean = Cv2.Mean(cornerRoi);

        Assert.InRange(mean.Val0, customColor.Val0 - 1, customColor.Val0 + 1);
        Assert.InRange(mean.Val1, customColor.Val1 - 1, customColor.Val1 + 1);
        Assert.InRange(mean.Val2, customColor.Val2 - 1, customColor.Val2 + 1);
    }

    [Fact]
    public void FillSolidColor_Blurred_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(source, new Rect(5, 5, 20, 15), new Scalar(10, 200, 30), thickness: -1);
        var padding = new CanvasPadding(8, 8, 8, 8);

        using var result = service.FillSolidColor(source, padding, blurred: true, blurRadius: 15);

        Assert.Equal(source.Width + padding.Left + padding.Right, result.Width);
        Assert.Equal(source.Height + padding.Top + padding.Bottom, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillReplicate_ReturnsExpectedSize_AndReplicatesEdge()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, new Scalar(45, 90, 135));
        var padding = new CanvasPadding(10, 10, 10, 10);

        using var result = service.FillReplicate(source, padding);

        Assert.Equal(60, result.Width);
        Assert.Equal(50, result.Height);

        using var cornerRoi = new Mat(result, new Rect(0, 0, padding.Left, padding.Top));
        var mean = Cv2.Mean(cornerRoi);
        Assert.InRange(mean.Val0, 44, 46);
        Assert.InRange(mean.Val1, 89, 91);
        Assert.InRange(mean.Val2, 134, 136);
    }

    [Fact]
    public void FillWrap_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, new Scalar(80, 100, 120));
        var padding = new CanvasPadding(12, 6, 12, 6);

        using var result = service.FillWrap(source, padding);

        Assert.Equal(64, result.Width);
        Assert.Equal(42, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillMirror_WithBlurAndFade_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = new Mat(30, 40, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(source, new Rect(5, 5, 20, 15), new Scalar(10, 200, 30), thickness: -1);
        var padding = new CanvasPadding(10, 10, 10, 10);

        using var result = service.FillMirror(source, padding, UncropMirrorType.Reflect101, blurRadius: 11, fadeOpacity: 0.5);
        Assert.Equal(60, result.Width);
        Assert.Equal(50, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillInpaint_WithPreFill_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, Scalar.All(120));
        var padding = new CanvasPadding(6, 4, 6, 4);

        using var result = service.FillInpaint(source, padding, UncropInpaintMethod.Telea, inpaintRadius: 8, blendMargin: 0, preFillEdgeAverage: true);

        Assert.Equal(52, result.Width);
        Assert.Equal(38, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillReplicate_WithSmoothRadius_ReturnsExpectedSize()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(40, 30, new Scalar(45, 90, 135));
        var padding = new CanvasPadding(10, 10, 10, 10);

        using var result = service.FillReplicate(source, padding, smoothRadius: 9);

        Assert.Equal(60, result.Width);
        Assert.Equal(50, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillZoomBlur_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = new Mat(40, 60, MatType.CV_8UC3, Scalar.All(100));
        Cv2.Circle(source, new Point(30, 20), 10, new Scalar(200, 50, 50), thickness: -1);
        var padding = new CanvasPadding(15, 10, 15, 10);

        using var result = service.FillZoomBlur(source, padding, blurRadius: 21, zoomScale: 1.3, blendMargin: 0);

        Assert.Equal(90, result.Width);
        Assert.Equal(60, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Theory]
    [InlineData(UncropGradientMode.PerEdgeSplay)]
    [InlineData(UncropGradientMode.FadeToColor)]
    [InlineData(UncropGradientMode.FourCorners)]
    public void FillEdgeGradient_SupportsAllModes_AndPreservesInterior(UncropGradientMode mode)
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(50, 40, new Scalar(100, 150, 200));
        var padding = new CanvasPadding(8, 8, 8, 8);

        using var result = service.FillEdgeGradient(source, padding, mode, customEndColor: new Scalar(0, 0, 0), noiseAmount: 0.05);

        Assert.Equal(66, result.Width);
        Assert.Equal(56, result.Height);

        using var centerRoi = new Mat(result, new Rect(padding.Left, padding.Top, source.Width, source.Height));
        Assert.True(MatsAreEqual(centerRoi, source));
    }

    [Fact]
    public void FillPatchSynthesis_ReturnsExpectedSize_AndPreservesInterior()
    {
        var service = new UncropFillService();
        using var source = MakeUniformImage(60, 60, new Scalar(50, 100, 150));
        var padding = new CanvasPadding(12, 12, 12, 12);

        using var result = service.FillPatchSynthesis(source, padding, patchSize: 16, blendOverlap: 4, blendMargin: 0);

        Assert.Equal(84, result.Width);
        Assert.Equal(84, result.Height);

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
