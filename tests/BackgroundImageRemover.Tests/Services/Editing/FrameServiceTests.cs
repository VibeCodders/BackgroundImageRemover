using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services.Editing;

/// <summary>
/// Covers the BGRA border/padding/vignette/bevel/corner-rounding effects in <see cref="FrameService"/>.
/// All target images are BGRA (CV_8UC4), so pixel comparisons are done locally via Vec4b rather than
/// the shared ServiceTestHelper (which only understands 1/3-channel Mats).
/// </summary>
public class FrameServiceTests
{
    private static Mat MakeBgra(int width, int height, Vec4b color)
    {
        var mat = new Mat(height, width, MatType.CV_8UC4, new Scalar(color.Item0, color.Item1, color.Item2, color.Item3));
        return mat;
    }

    private static Vec4b At(Mat m, int y, int x) => m.Get<Vec4b>(y, x);

    // ------------------------------------------------------------------ AddBorder

    [Fact]
    public void AddBorder_ExpandsCanvasBySymmetricThickness()
    {
        using var src = MakeBgra(10, 6, new Vec4b(10, 20, 30, 255));

        using var result = FrameService.AddBorder(src, 3, new Vec3b(0, 0, 255), opacity: 1.0);

        Assert.Equal(new Size(16, 12), result.Size());
        Assert.Equal(MatType.CV_8UC4, result.Type());
        // Border pixel (top-left corner) should be the border color, fully opaque.
        Assert.Equal(new Vec4b(0, 0, 255, 255), At(result, 0, 0));
        // Interior should retain the original content, offset by thickness.
        Assert.Equal(new Vec4b(10, 20, 30, 255), At(result, 3, 3));
    }

    [Fact]
    public void AddBorder_ZeroThickness_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(5, 5, new Vec4b(1, 2, 3, 255));

        using var result = FrameService.AddBorder(src, 0, new Vec3b(9, 9, 9), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void AddBorder_NegativeThickness_TreatedAsZero()
    {
        using var src = MakeBgra(5, 5, new Vec4b(1, 2, 3, 255));

        using var result = FrameService.AddBorder(src, -5, new Vec3b(9, 9, 9), 1.0);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void AddBorder_OpacityZero_BorderIsFullyTransparent()
    {
        using var src = MakeBgra(4, 4, new Vec4b(10, 10, 10, 255));

        using var result = FrameService.AddBorder(src, 2, new Vec3b(255, 0, 0), opacity: 0.0);

        Assert.Equal((byte)0, At(result, 0, 0).Item3);
    }

    // ------------------------------------------------------------------ AddInnerBorder

    [Fact]
    public void AddInnerBorder_DrawsAccentNearEdge()
    {
        using var src = MakeBgra(30, 30, new Vec4b(0, 0, 0, 255));

        using var result = FrameService.AddInnerBorder(src, 2, new Vec3b(255, 255, 255), opacity: 1.0);

        AssertPreservesSizeAndType(src, result);
        // Something near the top-left edge should have changed toward white.
        var edge = At(result, 0, 0);
        Assert.True(edge.Item0 > 0 || edge.Item1 > 0 || edge.Item2 > 0);
        // Center should be unaffected (rectangle outline only, not filled).
        Assert.Equal(new Vec4b(0, 0, 0, 255), At(result, 15, 15));
    }

    [Fact]
    public void AddInnerBorder_ZeroOpacity_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(10, 10, new Vec4b(5, 5, 5, 255));

        using var result = FrameService.AddInnerBorder(src, 2, new Vec3b(255, 0, 0), opacity: 0.0);

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void AddInnerBorder_ThicknessClampedToMinimumOne()
    {
        using var src = MakeBgra(10, 10, new Vec4b(0, 0, 0, 255));

        // thickness 0 should still draw a 1px line (Math.Max(1, thickness)), not be a no-op.
        using var result = FrameService.AddInnerBorder(src, 0, new Vec3b(255, 255, 255), opacity: 1.0);

        var edge = At(result, 0, 0);
        Assert.True(edge.Item0 > 0);
    }

    // ------------------------------------------------------------------ RoundCorners

    [Fact]
    public void RoundCorners_ZeroRadius_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(20, 20, new Vec4b(1, 2, 3, 255));

        using var result = FrameService.RoundCorners(src, 0);

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void RoundCorners_MakesExtremeCornerPixelTransparent()
    {
        using var src = MakeBgra(40, 40, new Vec4b(1, 2, 3, 255));

        using var result = FrameService.RoundCorners(src, 10);

        Assert.Equal((byte)0, At(result, 0, 0).Item3);
        Assert.Equal(new Vec4b(0, 0, 0, 0), At(result, 0, 0));
        // Center is untouched.
        Assert.Equal(new Vec4b(1, 2, 3, 255), At(result, 20, 20));
    }

    [Fact]
    public void RoundCorners_RadiusLargerThanHalfMinDimension_IsClamped()
    {
        using var src = MakeBgra(20, 10, new Vec4b(1, 2, 3, 255));

        // Should not throw even though requested radius (100) far exceeds min(width,height)/2 (5).
        using var result = FrameService.RoundCorners(src, 100);

        AssertPreservesSizeAndType(src, result);
        Assert.Equal((byte)0, At(result, 0, 0).Item3);
    }

    [Fact]
    public void RoundCorners_NegativeRadius_TreatedAsZero()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 2, 3, 255));

        using var result = FrameService.RoundCorners(src, -5);

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void RoundCorners_OnePixelImage_DoesNotThrow()
    {
        using var src = MakeBgra(1, 1, new Vec4b(9, 9, 9, 255));

        using var result = FrameService.RoundCorners(src, 5);

        Assert.Equal(new Size(1, 1), result.Size());
    }

    // ------------------------------------------------------------------ AddPadding / AddPaddingWithColor

    [Fact]
    public void AddPadding_ExpandsCanvasWithTransparentMargins()
    {
        using var src = MakeBgra(10, 8, new Vec4b(50, 60, 70, 255));

        using var result = FrameService.AddPadding(src, top: 2, right: 3, bottom: 4, left: 1);

        Assert.Equal(new Size(10 + 3 + 1, 8 + 2 + 4), result.Size());
        Assert.Equal(new Vec4b(0, 0, 0, 0), At(result, 0, 0));
        Assert.Equal(new Vec4b(50, 60, 70, 255), At(result, 2, 1));
    }

    [Fact]
    public void AddPadding_AllZero_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(5, 5, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPadding(src, 0, 0, 0, 0);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void AddPadding_NegativeValues_TreatedAsZero()
    {
        using var src = MakeBgra(5, 5, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPadding(src, -1, -2, -3, -4);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void AddPaddingWithColor_FillsMarginsWithOpaqueColor()
    {
        using var src = MakeBgra(6, 6, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPaddingWithColor(src, 2, 0, 0, 0, new Vec3b(10, 20, 30));

        Assert.Equal(new Vec4b(10, 20, 30, 255), At(result, 0, 0));
    }

    // ------------------------------------------------------------------ AddPartialBorder

    [Fact]
    public void AddPartialBorder_OnlyExpandsRequestedSides()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPartialBorder(src, 3, new Vec3b(200, 0, 0), 1.0, top: true, right: false, bottom: false, left: false);

        Assert.Equal(new Size(10, 13), result.Size());
        Assert.Equal(new Vec4b(200, 0, 0, 255), At(result, 0, 0));
        Assert.Equal(new Vec4b(1, 1, 1, 255), At(result, 3, 0));
    }

    [Fact]
    public void AddPartialBorder_NoSidesSelected_OnlyThicknessAffectsResult_NoExpansion()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPartialBorder(src, 3, new Vec3b(200, 0, 0), 1.0, top: false, right: false, bottom: false, left: false);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(new Vec4b(1, 1, 1, 255), At(result, 0, 0));
    }

    [Fact]
    public void AddPartialBorder_ZeroThickness_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPartialBorder(src, 0, new Vec3b(200, 0, 0), 1.0, true, true, true, true);

        Assert.Equal(src.Size(), result.Size());
    }

    // ------------------------------------------------------------------ AddGradientBorder

    [Fact]
    public void AddGradientBorder_TopLeftIsColorA_BottomRightIsColorB()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));
        var colorA = new Vec3b(0, 0, 0);
        var colorB = new Vec3b(255, 255, 255);

        using var result = FrameService.AddGradientBorder(src, 4, colorA, colorB, opacity: 1.0);

        var topLeft = At(result, 0, 0);
        var bottomRight = At(result, result.Height - 1, result.Width - 1);
        Assert.True(topLeft.Item0 < bottomRight.Item0);
        Assert.True(topLeft.Item1 < bottomRight.Item1);
        Assert.True(topLeft.Item2 < bottomRight.Item2);
    }

    [Fact]
    public void AddGradientBorder_ZeroThickness_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(6, 6, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddGradientBorder(src, 0, new Vec3b(0, 0, 0), new Vec3b(255, 255, 255));

        Assert.Equal(src.Size(), result.Size());
    }

    // ------------------------------------------------------------------ AddBevel

    [Fact]
    public void AddBevel_DrawsHighlightTopLeftAndShadowBottomRight()
    {
        using var src = MakeBgra(30, 30, new Vec4b(50, 50, 50, 255));

        using var result = FrameService.AddBevel(src, 2, new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), opacity: 1.0);

        AssertPreservesSizeAndType(src, result);
        var topLeft = At(result, 0, 0);
        var bottomRight = At(result, 29, 29);
        // Highlight should brighten, shadow should darken, relative to the base gray.
        Assert.True(topLeft.Item0 >= 50);
        Assert.True(bottomRight.Item0 <= 50);
    }

    [Fact]
    public void AddBevel_ZeroThickness_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddBevel(src, 0, new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), 1.0);

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void AddBevel_ZeroOpacity_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddBevel(src, 3, new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), 0.0);

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void AddBevel_ThicknessLargerThanImage_DoesNotThrow()
    {
        using var src = MakeBgra(5, 5, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddBevel(src, 100, new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), 1.0);

        Assert.Equal(src.Size(), result.Size());
    }

    // ------------------------------------------------------------------ AddPolaroidBar

    [Fact]
    public void AddPolaroidBar_AddsBarBelowImage()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPolaroidBar(src, 4, new Vec3b(255, 255, 255), opacity: 1.0);

        Assert.Equal(new Size(10, 14), result.Size());
        Assert.Equal(new Vec4b(255, 255, 255, 255), At(result, 12, 5));
        Assert.Equal(new Vec4b(1, 1, 1, 255), At(result, 0, 0));
    }

    [Fact]
    public void AddPolaroidBar_ZeroHeight_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(10, 10, new Vec4b(1, 1, 1, 255));

        using var result = FrameService.AddPolaroidBar(src, 0, new Vec3b(255, 255, 255));

        Assert.Equal(src.Size(), result.Size());
    }

    // ------------------------------------------------------------------ AddVignette

    [Fact]
    public void AddVignette_ZeroStrength_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgra(20, 20, new Vec4b(200, 200, 200, 255));

        using var result = FrameService.AddVignette(src, 0.0, new Vec3b(0, 0, 0));

        Assert.Equal(At(src, 0, 0), At(result, 0, 0));
    }

    [Fact]
    public void AddVignette_FullStrength_CornerApproachesTargetColor()
    {
        // Regression test: colorValues used to be normalized to 0..1 and multiplied directly
        // against a 0..255-scale factor product, so the vignette color barely registered
        // (corner pixels stayed near-black regardless of the requested color). With the fix,
        // a corner pixel at full vignette strength should end up close to the target color.
        using var src = MakeBgra(101, 101, new Vec4b(255, 255, 255, 255));
        var target = new Vec3b(0, 0, 255); // pure red channel (B=0,G=0,R=255)

        using var result = FrameService.AddVignette(src, 1.0, target);

        var corner = At(result, 0, 0);
        Assert.True(corner.Item0 < 40, $"Expected B near 0, got {corner.Item0}");
        Assert.True(corner.Item1 < 40, $"Expected G near 0, got {corner.Item1}");
        Assert.True(corner.Item2 > 200, $"Expected R near 255, got {corner.Item2}");
        // Alpha channel must be untouched by the vignette.
        Assert.Equal((byte)255, corner.Item3);
    }

    [Fact]
    public void AddVignette_CenterIsLessAffectedThanCorner()
    {
        using var src = MakeBgra(101, 101, new Vec4b(255, 255, 255, 255));
        var target = new Vec3b(0, 0, 0);

        using var result = FrameService.AddVignette(src, 1.0, target);

        var center = At(result, 50, 50);
        var corner = At(result, 0, 0);
        // Center should remain closer to white (255) than the corner, which is pulled toward black.
        Assert.True(center.Item0 > corner.Item0);
    }

    [Fact]
    public void AddVignette_PreservesSizeAndType()
    {
        using var src = MakeBgra(15, 9, new Vec4b(120, 130, 140, 255));

        using var result = FrameService.AddVignette(src, 0.5, new Vec3b(0, 0, 0));

        AssertPreservesSizeAndType(src, result);
    }

    // ------------------------------------------------------------------ shared helpers

    private static void AssertPreservesSizeAndType(Mat input, Mat result)
    {
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
