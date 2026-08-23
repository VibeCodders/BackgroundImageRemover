using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services.Editing;

/// <summary>
/// Covers <see cref="EmojiOverlayService"/>: drawing decorative emoji glyphs (star, heart,
/// sparkles, etc.) onto a BGR image at a position/size/color/opacity, and scattering many of them.
/// </summary>
public class EmojiOverlayServiceTests
{
    private static Mat MakeBgr(int width, int height, Vec3b color)
        => new(height, width, MatType.CV_8UC3, new Scalar(color.Item0, color.Item1, color.Item2));

    private static bool AnyPixelDiffers(Mat a, Mat b)
    {
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        return Cv2.CountNonZero(gray) > 0;
    }

    // ------------------------------------------------------------------ Render: basics

    [Fact]
    public void Render_Circle_PaintsColorAtCenterWhenFullyOpaque()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));
        var color = new Vec3b(0, 255, 0);

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(50, 50), 40, color, opacity: 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
        Assert.Equal(color, result.Get<Vec3b>(50, 50));
    }

    [Fact]
    public void Render_OpacityZero_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(50, 50), 40, new Vec3b(255, 255, 255), opacity: 0.0);

        Assert.False(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_HalfOpacity_BlendsWithBackground()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));
        var color = new Vec3b(200, 200, 200);

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(50, 50), 40, color, opacity: 0.5);

        var px = result.Get<Vec3b>(50, 50);
        // Should be roughly halfway between black background and the emoji color, not exactly
        // either endpoint.
        Assert.True(px.Item0 > 50 && px.Item0 < 180, $"Expected a blended value, got {px.Item0}");
    }

    [Fact]
    public void Render_SizeBelowMinimum_IsClampedNotThrown()
    {
        using var src = MakeBgr(50, 50, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Star, new Point(25, 25), 0, new Vec3b(255, 0, 0), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_PositionFullyOffCanvas_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(50, 50, new Vec3b(10, 10, 10));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(-100, -100), 20, new Vec3b(255, 0, 0), 1.0);

        Assert.False(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_PositionPartiallyOffTopLeftCorner_ClipsWithoutThrowing()
    {
        using var src = MakeBgr(50, 50, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(0, 0), 30, new Vec3b(255, 0, 0), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_PositionPartiallyOffBottomRightCorner_ClipsWithoutThrowing()
    {
        using var src = MakeBgr(50, 50, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(49, 49), 30, new Vec3b(255, 0, 0), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_OneByOneImage_DoesNotThrow()
    {
        using var src = MakeBgr(1, 1, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Circle, new Point(0, 0), 8, new Vec3b(255, 0, 0), 1.0);

        Assert.Equal(new Size(1, 1), result.Size());
    }

    [Theory]
    [InlineData(EmojiOverlayService.EmojiKind.Star)]
    [InlineData(EmojiOverlayService.EmojiKind.Heart)]
    [InlineData(EmojiOverlayService.EmojiKind.Sparkles)]
    [InlineData(EmojiOverlayService.EmojiKind.Circle)]
    [InlineData(EmojiOverlayService.EmojiKind.Diamond)]
    [InlineData(EmojiOverlayService.EmojiKind.Triangle)]
    [InlineData(EmojiOverlayService.EmojiKind.Boom)]
    [InlineData(EmojiOverlayService.EmojiKind.Peace)]
    [InlineData(EmojiOverlayService.EmojiKind.Arrow)]
    [InlineData(EmojiOverlayService.EmojiKind.Cross)]
    public void Render_AllEmojiKinds_ProduceChangesWithoutThrowing(EmojiOverlayService.EmojiKind kind)
    {
        using var src = MakeBgr(80, 80, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.Render(src, kind, new Point(40, 40), 40, new Vec3b(255, 255, 255), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    // ------------------------------------------------------------------ RenderScatter

    [Fact]
    public void RenderScatter_ZeroCount_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Star, 0, 10, 20, new Vec3b(255, 255, 255), 1.0);

        Assert.False(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void RenderScatter_PositiveCount_ChangesImageAndPreservesSize()
    {
        using var src = MakeBgr(200, 200, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Star, 15, 10, 20, new Vec3b(255, 255, 255), 1.0);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void RenderScatter_UsesFixedSeed_IsDeterministicAcrossCalls()
    {
        using var src = MakeBgr(200, 200, new Vec3b(0, 0, 0));

        using var first = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Sparkles, 10, 8, 16, new Vec3b(0, 255, 255), 1.0);
        using var second = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Sparkles, 10, 8, 16, new Vec3b(0, 255, 255), 1.0);

        Assert.False(AnyPixelDiffers(first, second));
    }

    [Fact]
    public void RenderScatter_MinSizeEqualsMaxSize_DoesNotThrow()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Circle, 5, 12, 12, new Vec3b(255, 0, 0), 1.0);

        Assert.Equal(src.Size(), result.Size());
    }
}
