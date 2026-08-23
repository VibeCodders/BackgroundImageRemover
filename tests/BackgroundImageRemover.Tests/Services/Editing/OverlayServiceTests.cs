using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services.Editing;

/// <summary>
/// Covers <see cref="OverlayService"/>: compositing a BGRA overlay (logo/sticker) over a BGR base
/// with scale, opacity, anchor, rotation, flip, tint, drop shadow and blend modes.
/// </summary>
public class OverlayServiceTests
{
    private static Mat MakeBgr(int width, int height, Vec3b color)
        => new(height, width, MatType.CV_8UC3, new Scalar(color.Item0, color.Item1, color.Item2));

    private static Mat MakeOpaqueBgra(int width, int height, Vec3b color)
        => new(height, width, MatType.CV_8UC4, new Scalar(color.Item0, color.Item1, color.Item2, 255));

    // ------------------------------------------------------------------ Basic compositing

    [Fact]
    public void Composite_PlacesOverlayAtRequestedAnchor_TopLeft()
    {
        using var baseImg = MakeBgr(100, 100, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, scale: 1.0, opacity: 1.0, margin: 0);

        Assert.Equal(baseImg.Size(), result.Size());
        Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(5, 5));
        // Far from the overlay, base should be untouched.
        Assert.Equal(new Vec3b(0, 0, 0), result.Get<Vec3b>(90, 90));
    }

    [Fact]
    public void Composite_BottomRightAnchor_PlacesOverlayNearBottomRightCorner()
    {
        using var baseImg = MakeBgr(100, 100, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.BottomRight, scale: 1.0, opacity: 1.0, margin: 0);

        Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(95, 95));
        Assert.Equal(new Vec3b(0, 0, 0), result.Get<Vec3b>(0, 0));
    }

    [Fact]
    public void Composite_MarginPushesOverlayAwayFromEdge()
    {
        using var baseImg = MakeBgr(50, 50, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, scale: 1.0, opacity: 1.0, margin: 5);

        // With margin 5, overlay starts at (5,5); pixel (2,2) should still be background.
        Assert.Equal(new Vec3b(0, 0, 0), result.Get<Vec3b>(2, 2));
        Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(8, 8));
    }

    [Fact]
    public void Composite_OpacityZero_LeavesBaseUnchanged()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(10, 10, 10));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.Center, scale: 1.0, opacity: 0.0, margin: 0);

        Assert.Equal(new Vec3b(10, 10, 10), result.Get<Vec3b>(10, 10));
    }

    [Fact]
    public void Composite_OpacityOne_FullyReplacesOverlaidPixels()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(10, 10, 10));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(200, 100, 50));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.Center, scale: 1.0, opacity: 1.0, margin: 0);

        Assert.Equal(new Vec3b(200, 100, 50), result.Get<Vec3b>(10, 10));
    }

    [Fact]
    public void Composite_TransparentOverlayPixels_DoNotAffectBase()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(10, 10, 10));
        using var overlay = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 0));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.Center, scale: 1.0, opacity: 1.0, margin: 0);

        Assert.Equal(new Vec3b(10, 10, 10), result.Get<Vec3b>(10, 10));
    }

    [Fact]
    public void Composite_ScaleBelowMinimum_IsClampedNotThrown()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, scale: 0.0, opacity: 1.0, margin: 0);

        Assert.Equal(baseImg.Size(), result.Size());
    }

    [Fact]
    public void Composite_NegativeMargin_TreatedAsZero()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(4, 4, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, scale: 1.0, opacity: 1.0, margin: -10);

        Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(0, 0));
    }

    // ------------------------------------------------------------------ Overlay larger than base / off-canvas placement

    [Fact]
    public void Composite_OverlayLargerThanBase_BottomRightAnchor_ShowsOverlaysBottomRightPortion()
    {
        // Regression test: BlendPrepared used to clamp a negative destination position to 0
        // without cropping the matching amount off the overlay's source origin, so an
        // oversized overlay anchored BottomRight incorrectly showed its top-left portion
        // instead of its bottom-right portion.
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = new Mat(40, 40, MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
        // Mark the overlay's bottom-right quadrant a distinct opaque color; everything else transparent.
        using (var quadrant = new Mat(overlay, new Rect(20, 20, 20, 20)))
        {
            quadrant.SetTo(new Scalar(0, 255, 0, 255));
        }

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.BottomRight, scale: 1.0, opacity: 1.0, margin: 0);

        // The visible portion of the oversized overlay on the base's bottom-right corner
        // should be the overlay's bottom-right (colored) quadrant, not its transparent top-left.
        Assert.Equal(new Vec3b(0, 255, 0), result.Get<Vec3b>(19, 19));
    }

    [Fact]
    public void Composite_OverlayLargerThanBase_TopLeftAnchor_ShowsOverlaysTopLeftPortion()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = new Mat(40, 40, MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
        using (var quadrant = new Mat(overlay, new Rect(0, 0, 20, 20)))
        {
            quadrant.SetTo(new Scalar(0, 0, 255, 255));
        }

        using var result = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, scale: 1.0, opacity: 1.0, margin: 0);

        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(0, 0));
    }

    // ------------------------------------------------------------------ Flip / rotation / tint

    [Fact]
    public void Composite_FlipHorizontal_MirrorsOverlayContent()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = new Mat(10, 10, MatType.CV_8UC4, new Scalar(0, 0, 0, 255));
        // Left half red, right half blue so a horizontal flip is observable.
        using (var left = new Mat(overlay, new Rect(0, 0, 5, 10))) left.SetTo(new Scalar(0, 0, 255, 255));
        using (var right = new Mat(overlay, new Rect(5, 0, 5, 10))) right.SetTo(new Scalar(255, 0, 0, 255));

        using var flipped = OverlayService.Composite(baseImg, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0, rotation: 0.0, flipHorizontal: true);

        // After a horizontal flip, the (now) left side of the placed overlay should be blue.
        Assert.Equal(new Vec3b(255, 0, 0), flipped.Get<Vec3b>(5, 1));
        Assert.Equal(new Vec3b(0, 0, 255), flipped.Get<Vec3b>(5, 8));
    }

    [Fact]
    public void Composite_Tint_ScalesOverlayColorChannels()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(200, 200, 200));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false,
            tint: new Vec3b(255, 0, 0));

        var px = result.Get<Vec3b>(5, 5);
        // Blue channel tinted to full (255/255 factor -> unchanged ~200), G/R tinted toward 0.
        Assert.Equal(200, px.Item0);
        Assert.Equal(0, px.Item1);
        Assert.Equal(0, px.Item2);
    }

    [Fact]
    public void Composite_Rotation_ChangesOutputComparedToUnrotated()
    {
        using var baseImg = MakeBgr(60, 60, new Vec3b(0, 0, 0));
        using var overlay = MakeOpaqueBgra(20, 10, new Vec3b(255, 255, 255));

        using var unrotated = OverlayService.Composite(baseImg, overlay, TextAnchor.Center, 1.0, 1.0, 0, rotation: 0.0);
        using var rotated = OverlayService.Composite(baseImg, overlay, TextAnchor.Center, 1.0, 1.0, 0, rotation: 45.0);

        using var diff = new Mat();
        Cv2.Absdiff(unrotated, rotated, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);
    }

    // ------------------------------------------------------------------ Drop shadow

    [Fact]
    public void Composite_DropShadow_DarkensPixelsNearOverlayOffset()
    {
        using var baseImg = MakeBgr(60, 60, new Vec3b(200, 200, 200));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false, tint: null,
            dropShadow: true, shadowOffset: 6, shadowOpacity: 1.0);

        // Region offset by shadowOffset beyond the overlay's bottom-right, not covered by the
        // overlay itself, should be darkened by the shadow.
        var shadowedPixel = result.Get<Vec3b>(14, 14);
        Assert.True(shadowedPixel.Item0 < 200);
    }

    [Fact]
    public void Composite_NoDropShadow_LeavesSurroundingAreaUntouched()
    {
        using var baseImg = MakeBgr(60, 60, new Vec3b(200, 200, 200));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(255, 255, 255));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.TopLeft, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false, tint: null,
            dropShadow: false);

        Assert.Equal(new Vec3b(200, 200, 200), result.Get<Vec3b>(14, 14));
    }

    // ------------------------------------------------------------------ Blend modes

    [Theory]
    [InlineData(OverlayBlendMode.Multiply)]
    [InlineData(OverlayBlendMode.Screen)]
    [InlineData(OverlayBlendMode.Overlay)]
    [InlineData(OverlayBlendMode.Normal)]
    public void Composite_AllBlendModes_ProduceCorrectSizeWithoutThrowing(OverlayBlendMode mode)
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(120, 120, 120));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(200, 50, 10));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.Center, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false, tint: null,
            dropShadow: false, blend: mode);

        Assert.Equal(baseImg.Size(), result.Size());
    }

    [Fact]
    public void Composite_MultiplyBlend_DarkensRelativeToBase()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(200, 200, 200));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(100, 100, 100));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.Center, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false, tint: null,
            dropShadow: false, blend: OverlayBlendMode.Multiply);

        var px = result.Get<Vec3b>(10, 10);
        Assert.True(px.Item0 < 200);
    }

    [Fact]
    public void Composite_ScreenBlend_LightensRelativeToBase()
    {
        using var baseImg = MakeBgr(20, 20, new Vec3b(50, 50, 50));
        using var overlay = MakeOpaqueBgra(10, 10, new Vec3b(200, 200, 200));

        using var result = OverlayService.Composite(
            baseImg, overlay, TextAnchor.Center, 1.0, 1.0, 0,
            rotation: 0.0, flipHorizontal: false, flipVertical: false, tint: null,
            dropShadow: false, blend: OverlayBlendMode.Screen);

        var px = result.Get<Vec3b>(10, 10);
        Assert.True(px.Item0 > 50);
    }
}
