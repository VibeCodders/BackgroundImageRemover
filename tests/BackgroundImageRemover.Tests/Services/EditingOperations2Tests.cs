using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
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

    [Fact]
    public void TransformPad_ExpandsCanvasWithFill()
    {
        using var src = new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = TransformService.Pad(src, 2, 3, 4, 5, new Scalar(1, 2, 3));

        Assert.Equal(10, result.Width);
        Assert.Equal(12, result.Height);
        var corner = result.At<Vec3b>(0, 0);
        Assert.Equal(1, corner.Item0);
        Assert.Equal(2, corner.Item1);
        Assert.Equal(3, corner.Item2);
        var inner = result.At<Vec3b>(3, 3);
        Assert.Equal(10, inner.Item0);
        Assert.Equal(20, inner.Item1);
        Assert.Equal(30, inner.Item2);
    }

    [Fact]
    public void TransformResizeToFit_PreservesAspectAndDoesNotUpscale()
    {
        using var src = new Mat(100, 200, MatType.CV_8UC3);

        using var result = TransformService.ResizeToFit(src, maxWidth: 100, maxHeight: 100);

        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void TransformCropCenter_ReturnsExactCenteredSize()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(5, 5, 5));

        using var result = TransformService.CropCenter(src, 6, 4, new Scalar(0, 0, 0));

        Assert.Equal(6, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(5, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void TransformTile_RepeatsImageToFillCanvas()
    {
        using var src = new Mat(2, 2, MatType.CV_8UC3, new Scalar(7, 8, 9));

        using var result = TransformService.Tile(src, 5, 5);

        Assert.Equal(5, result.Width);
        Assert.Equal(5, result.Height);
        var px = result.At<Vec3b>(4, 4);
        Assert.Equal(7, px.Item0);
        Assert.Equal(8, px.Item1);
        Assert.Equal(9, px.Item2);
    }

    [Fact]
    public void TransformAutoStraighten_ReturnsImageWithoutThrowing()
    {
        using var src = new Mat(200, 200, MatType.CV_8UC3, Scalar.All(0));
        using (var bar = new Mat(src, new Rect(50, 90, 100, 20)))
        {
            bar.SetTo(new Scalar(255, 255, 255));
        }

        using var result = TransformService.AutoStraighten(src, maxAngle: 30);

        Assert.True(result.Width >= src.Width);
        Assert.True(result.Height >= src.Height);
    }

    [Fact]
    public void EstimateSkewAngle_FlatImage_ReturnsZero()
    {
        using var src = new Mat(200, 200, MatType.CV_8UC3, new Scalar(120, 120, 120));

        double angle = TransformService.EstimateSkewAngle(src, maxAngle: 30);

        Assert.Equal(0.0, angle, 3);
    }

    [Fact]
    public void TrimContent_CustomColor_RemovesThatColorBorder()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(src, new Rect(0, 0, 10, 10), new Scalar(255, 255, 255), -1);
        using (var content = new Mat(src, new Rect(2, 2, 6, 6)))
        {
            content.SetTo(new Scalar(10, 20, 30));
        }

        using var result = CropService.TrimContent(src, new Vec3b(255, 255, 255), tolerance: 12);

        Assert.Equal(new Size(6, 6), result.Size());
        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(10, px.Item0);
        Assert.Equal(20, px.Item1);
        Assert.Equal(30, px.Item2);
    }

    [Fact]
    public void CenteredRectForSize_ReturnsCenteredClampedRect()
    {
        var rect = CropService.CenteredRectForSize(new Size(100, 80), 60, 40);

        Assert.Equal(new Rect(20, 20, 60, 40), rect);
    }

    [Fact]
    public void AspectPresets_ContainGoldenRatio()
    {
        Assert.Contains(UncropAspectPresets.All, p => p.Label.StartsWith("Golden") && p.Ratio is > 1.6 and < 1.62);
    }

    [Fact]
    public void ResizeToHeight_PreservesAspect()
    {
        using var src = new Mat(100, 200, MatType.CV_8UC3); // 200 wide x 100 tall

        using var result = ResizeService.ResizeToHeight(src, 25, ResampleMethod.Linear);

        Assert.Equal(25, result.Height);
        Assert.Equal(50, result.Width);
    }

    [Fact]
    public void FitWithin_ScalesToLargestFittingBox()
    {
        using var src = new Mat(100, 200, MatType.CV_8UC3); // 200 wide x 100 tall

        using var result = ResizeService.FitWithin(src, 100, 100, ResampleMethod.Linear);

        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public void FillTo_CoversBoxAndCropsOverflow()
    {
        using var src = new Mat(100, 200, MatType.CV_8UC3); // 200 wide x 100 tall

        using var result = ResizeService.FillTo(src, 100, 100, ResampleMethod.Linear);

        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void ResizeToLongestSide_ScalesLargestDimension()
    {
        using var src = new Mat(100, 300, MatType.CV_8UC3); // 300 wide x 100 tall

        using var result = ResizeService.ResizeToLongestSide(src, 60, ResampleMethod.Linear);

        Assert.Equal(60, result.Width);
        Assert.Equal(20, result.Height);
    }

    [Fact]
    public void ResizeToMegapixels_TargetsArea()
    {
        using var src = new Mat(1000, 1000, MatType.CV_8UC3); // 1 MP

        using var result = ResizeService.ResizeToMegapixels(src, 0.25, ResampleMethod.Linear);

        Assert.Equal(500, result.Width);
        Assert.Equal(500, result.Height);
    }

    [Fact]
    public void SolidFill_FillsRegionWithColor()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));

        using var result = MosaicService.SolidFill(src, new Rect(2, 2, 4, 4), new Vec3b(0, 0, 255));

        Assert.Equal(255, result.At<Vec3b>(3, 3).Item2);
        Assert.Equal(0, result.At<Vec3b>(0, 0).Item2);
    }

    [Fact]
    public void MedianBlur_PreservesSizeAndUniformColor()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = MosaicService.MedianBlur(src, null, 5);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(100, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void PixelateOutside_KeepsSelectionAndCensorsTheRest()
    {
        using var src = new Mat(32, 32, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(src, new Rect(0, 0, 16, 16), new Scalar(255, 255, 255), -1);
        var region = new Rect(16, 16, 16, 16);

        using var result = MosaicService.PixelateOutside(src, region, cellSize: 32);

        Assert.Equal(0, result.At<Vec3b>(24, 24).Item0); // selection preserved
        Assert.True(result.At<Vec3b>(0, 0).Item0 < 255); // outside censored (bright block averaged)
    }

    [Fact]
    public void Crystallize_PreservesSizeAndType()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = MosaicService.Crystallize(src, null, cellSize: 4, jitter: 2);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void BlurSoft_ZeroStrength_ReturnsOriginal()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = MosaicService.BlurSoft(src, null, 5, 0.0);

        Assert.Equal(100, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void BlendByMask_AppliesModifiedOnlyWhereMaskIsSet()
    {
        using var original = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 10, 10));
        using var modified = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        using (var roi = new Mat(mask, new Rect(2, 2, 4, 4)))
        {
            roi.SetTo(new Scalar(255));
        }

        using var result = MosaicService.BlendByMask(original, modified, mask);

        Assert.Equal(200, result.At<Vec3b>(3, 3).Item0);
        Assert.Equal(10, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void Overlay_Rotation_PreservesBaseSize()
    {
        using var baseBgr = new Mat(40, 40, MatType.CV_8UC3, new Scalar(10, 10, 10));
        using var overlay = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.Center, 1.0, 1.0, 0, rotation: 45);

        Assert.Equal(baseBgr.Size(), result.Size());
    }

    [Fact]
    public void Overlay_FlipHorizontal_MovesContent()
    {
        using var baseBgr = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(0));
        using var overlay = new Mat(2, 2, MatType.CV_8UC4, Scalar.All(0));
        overlay.Set(0, 0, new Vec4b(255, 255, 255, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0, flipHorizontal: true);

        Assert.True(result.At<Vec3b>(0, 1).Item0 > 200);
        Assert.Equal(0, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void Overlay_Tint_ColorsWhiteOverlay()
    {
        using var baseBgr = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        using var overlay = new Mat(4, 4, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0, tint: new Vec3b(0, 0, 255));

        var px = result.At<Vec3b>(2, 2);
        Assert.Equal(0, px.Item0);
        Assert.Equal(0, px.Item1);
        Assert.True(px.Item2 > 200);
    }

    [Fact]
    public void Overlay_DropShadow_DarkensBelowOverlay()
    {
        using var baseBgr = new Mat(20, 20, MatType.CV_8UC3, new Scalar(255, 255, 255));
        using var overlay = new Mat(4, 4, MatType.CV_8UC4, new Scalar(0, 0, 0, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0, dropShadow: true, shadowOffset: 4, shadowOpacity: 1.0);

        Assert.True(result.At<Vec3b>(6, 6).Item0 < 128);
    }

    [Fact]
    public void Overlay_MultiplyBlend_Darkens()
    {
        using var baseBgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var overlay = new Mat(4, 4, MatType.CV_8UC4, new Scalar(100, 100, 100, 255));

        using var result = OverlayService.Composite(baseBgr, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0, blend: OverlayBlendMode.Multiply);

        Assert.True(result.At<Vec3b>(2, 2).Item0 < 200);
    }

    [Fact]
    public void Levels_OutputLevels_ClampToRange()
    {
        using var src = new Mat(1, 2, MatType.CV_8UC3);
        src.Set(0, 0, new Vec3b(0, 0, 0));
        src.Set(0, 1, new Vec3b(255, 255, 255));

        using var result = LevelsService.Apply(src, 0, 255, 1.0, LevelsChannel.Rgb, outputBlack: 50, outputWhite: 200);

        Assert.Equal(50, result.At<Vec3b>(0, 0).Item0);
        Assert.Equal(200, result.At<Vec3b>(0, 1).Item0);
    }

    [Fact]
    public void AutoLevels_StretchesToFullRange()
    {
        using var src = new Mat(1, 3, MatType.CV_8UC3);
        src.Set(0, 0, new Vec3b(100, 100, 100));
        src.Set(0, 1, new Vec3b(150, 150, 150));
        src.Set(0, 2, new Vec3b(200, 200, 200));

        using var result = LevelsService.AutoLevels(src);

        Assert.Equal(0, result.At<Vec3b>(0, 0).Item0);
        Assert.Equal(255, result.At<Vec3b>(0, 2).Item0);
    }

    [Fact]
    public void Equalize_PreservesSizeAndType()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = LevelsService.Equalize(src);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void Invert_FlipsValues()
    {
        using var src = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 100, 200));

        using var result = LevelsService.Invert(src);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(245, px.Item0);
        Assert.Equal(155, px.Item1);
        Assert.Equal(55, px.Item2);
    }

    [Fact]
    public void AutoWhiteBalance_NeutralGrayUnchanged()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(120, 120, 120));

        using var result = LevelsService.AutoWhiteBalance(src);

        var px = result.At<Vec3b>(5, 5);
        Assert.Equal(120, px.Item0);
        Assert.Equal(120, px.Item1);
        Assert.Equal(120, px.Item2);
    }
}
