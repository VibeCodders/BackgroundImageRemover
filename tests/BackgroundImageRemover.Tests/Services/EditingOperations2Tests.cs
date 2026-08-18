using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class EditingOperations2Tests
{
    [Fact]
    public void CropRect_ExtractsTheRequestedRegion()
    {
        using var src = new Mat(4, 4, MatType.CV_8UC3, Scalar.All(0));

        using var result = CropService.CropRect(src, new Rect(1, 1, 2, 2));

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
    }

    [Fact]
    public void CenteredRectForAspect_ReturnsCenteredCrop()
    {
        var rect = CropService.CenteredRectForAspect(new Size(100, 100), 2.0);

        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
        Assert.Equal(0, rect.X);
        Assert.Equal(25, rect.Y);
    }

    [Fact]
    public void TrimContentBounds_FindsContentInsideFlatBorder()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        using (var content = new Mat(src, new Rect(3, 3, 4, 4)))
        {
            content.SetTo(new Scalar(255, 255, 255));
        }

        var bounds = CropService.TrimContentBounds(src, tolerance: 12);

        Assert.Equal(new Rect(3, 3, 4, 4), bounds);
    }

    [Fact]
    public void ResizeTo_ProducesExactDimensions()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3);

        using var result = ResizeService.ResizeTo(src, 20, 5, ResampleMethod.Linear);

        Assert.Equal(20, result.Width);
        Assert.Equal(5, result.Height);
    }

    [Fact]
    public void ResizePercent_ScalesByPercentage()
    {
        using var src = new Mat(100, 100, MatType.CV_8UC3);

        using var result = ResizeService.ResizePercent(src, 0.5);

        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void Pixelate_BlocksRegionsAndPreservesSize()
    {
        // Left half black, right half white.
        using var src = new Mat(16, 16, MatType.CV_8UC3, Scalar.All(0));
        using (var right = new Mat(src, new Rect(8, 0, 8, 16)))
        {
            right.SetTo(new Scalar(255, 255, 255));
        }

        using var result = MosaicService.Pixelate(src, region: null, cellSize: 8);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(0, result.At<Vec3b>(4, 4).Item0);    // black quadrant
        Assert.Equal(255, result.At<Vec3b>(12, 12).Item0); // white quadrant
    }

    [Fact]
    public void Blur_PreservesSizeAndUniformColor()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = MosaicService.Blur(src, region: null, radius: 5);

        Assert.Equal(src.Size(), result.Size());
        var px = result.At<Vec3b>(5, 5);
        Assert.Equal(100, px.Item0);
        Assert.Equal(100, px.Item1);
        Assert.Equal(100, px.Item2);
    }

    [Fact]
    public void Overlay_CompositesOpaqueOverlayInCorner()
    {
        using var baseBgr = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        using var overlay = new Mat(2, 2, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.BottomRight, scale: 1.0, opacity: 1.0, margin: 0);

        var corner = result.At<Vec3b>(8, 8);
        Assert.Equal(255, corner.Item0);
        Assert.Equal(255, corner.Item1);
        Assert.Equal(255, corner.Item2);

        var far = result.At<Vec3b>(0, 0);
        Assert.Equal(0, far.Item0);
    }

    [Fact]
    public void Levels_MapsMidtoneBetweenBlackAndWhitePoints()
    {
        using var src = new Mat(1, 1, MatType.CV_8UC3, new Scalar(128, 128, 128));

        using var result = LevelsService.Apply(src, blackPoint: 50, whitePoint: 200, gamma: 1.0, LevelsChannel.Rgb);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(133, px.Item0);
        Assert.Equal(133, px.Item1);
        Assert.Equal(133, px.Item2);
    }

    [Fact]
    public void TransformSkew_ExpandsCanvas()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = TransformService.Skew(src, skewX: 45, skewY: 0);

        // tan(45°) = 1, so the width grows by the height.
        Assert.Equal(20, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void TransformResizeTo_ProducesExactDimensions()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3);

        using var result = TransformService.ResizeTo(src, 20, 5);

        Assert.Equal(20, result.Width);
        Assert.Equal(5, result.Height);
    }

    [Fact]
    public void TransformCropToAspect_ReturnsCenteredCrop()
    {
        using var src = new Mat(100, 100, MatType.CV_8UC3);

        using var result = TransformService.CropToAspect(src, 2.0);

        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void TransformTrimBorder_RemovesFlatBorder()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        using (var content = new Mat(src, new Rect(3, 3, 4, 4)))
        {
            content.SetTo(new Scalar(255, 255, 255));
        }

        using var result = TransformService.TrimBorder(src, tolerance: 12);

        Assert.Equal(4, result.Width);
        Assert.Equal(4, result.Height);
    }
}
