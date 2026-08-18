using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class EditingOperationsTests
{
    [Fact]
    public void Grayscale_EqualizesChannels_AtFullIntensity()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red

        using var result = FilterService.Apply(input, FilterKind.Grayscale, intensity: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(px.Item0, px.Item1);
        Assert.Equal(px.Item1, px.Item2);
    }

    [Fact]
    public void Invert_FlipsChannelValues()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = FilterService.Apply(input, FilterKind.Invert, intensity: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(245, px.Item0);
        Assert.Equal(235, px.Item1);
        Assert.Equal(225, px.Item2);
    }

    [Fact]
    public void Posterize_QuantizesChannelValues()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(130, 130, 130));

        // 4 levels -> bucket of 64 -> 130 maps to 128.
        using var result = FilterService.Apply(input, FilterKind.Posterize, intensity: 1.0, posterizeLevels: 4);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(128, px.Item0);
        Assert.Equal(128, px.Item1);
        Assert.Equal(128, px.Item2);
    }

    [Fact]
    public void IntensityZero_ReturnsTheOriginal()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(0, 0, 255));

        using var result = FilterService.Apply(input, FilterKind.Grayscale, intensity: 0.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(0, px.Item0);
        Assert.Equal(0, px.Item1);
        Assert.Equal(255, px.Item2);
    }

    [Fact]
    public void FlipHorizontal_SwapsLeftAndRight()
    {
        using var input = new Mat(1, 2, MatType.CV_8UC3);
        input.Set(0, 0, new Vec3b(0, 0, 255));    // left red
        input.Set(0, 1, new Vec3b(255, 0, 0));    // right blue

        using var result = TransformService.FlipHorizontal(input);

        Assert.Equal(255, result.At<Vec3b>(0, 0).Item0); // now blue on the left
        Assert.Equal(255, result.At<Vec3b>(0, 1).Item2); // and red on the right
    }

    [Fact]
    public void Rotate90Clockwise_SwapsDimensions()
    {
        using var input = new Mat(2, 3, MatType.CV_8UC3);

        using var result = TransformService.Rotate90Clockwise(input);

        Assert.Equal(2, result.Width);
        Assert.Equal(3, result.Height);
    }

    [Fact]
    public void Resize_ScalesByFactor()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC3);

        using var result = TransformService.Resize(input, 0.5);

        Assert.Equal(5, result.Width);
        Assert.Equal(5, result.Height);
    }

    [Fact]
    public void AddBorder_ExpandsCanvasAndFillsBorder()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddBorder(input, thickness: 2, new Vec3b(0, 0, 255));

        Assert.Equal(14, result.Width);
        Assert.Equal(14, result.Height);

        var corner = result.At<Vec4b>(0, 0);
        Assert.Equal(0, corner.Item0);
        Assert.Equal(0, corner.Item1);
        Assert.Equal(255, corner.Item2); // red border
        Assert.Equal(255, corner.Item3);

        var center = result.At<Vec4b>(7, 7);
        Assert.Equal(255, center.Item0); // original white content
        Assert.Equal(255, center.Item1);
        Assert.Equal(255, center.Item2);
    }

    [Fact]
    public void RoundCorners_TransparentizesCorners()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.RoundCorners(input, radius: 4);

        Assert.Equal(0, result.At<Vec4b>(0, 0).Item3); // corner alpha cleared
        Assert.Equal(255, result.At<Vec4b>(5, 5).Item3); // center stays opaque
    }

    [Fact]
    public void AddPadding_ExpandsCanvasTransparent()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddPadding(input, top: 3, right: 3, bottom: 3, left: 3);

        Assert.Equal(16, result.Width);
        Assert.Equal(16, result.Height);
        Assert.Equal(0, result.At<Vec4b>(0, 0).Item3); // new corner transparent
        Assert.Equal(255, result.At<Vec4b>(3, 3).Item3); // original content opaque
    }

    [Fact]
    public void TextOverlay_BlankText_LeavesImageUnchanged()
    {
        using var input = new Mat(40, 40, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = TextOverlayService.Render(input, "", TextAnchor.Center, 40, new Vec3b(255, 255, 255), 1.0, 10);

        Assert.Equal(40, result.Width);
        Assert.Equal(40, result.Height);
        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(10, px.Item0);
        Assert.Equal(20, px.Item1);
        Assert.Equal(30, px.Item2);
    }

    [Fact]
    public void TextOverlay_WithText_ModifiesOnlyTheTargetRegion()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));

        using var result = TextOverlayService.Render(input, "TEST", TextAnchor.BottomRight, 48, new Vec3b(255, 255, 255), 1.0, 10);

        Assert.Equal(input.Size(), result.Size());

        // Some pixels in the bottom-right were painted white.
        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);

        // The top-left corner (far from the watermark) is untouched.
        var corner = result.At<Vec3b>(0, 0);
        Assert.Equal(0, corner.Item0);
        Assert.Equal(0, corner.Item1);
        Assert.Equal(0, corner.Item2);
    }
}
