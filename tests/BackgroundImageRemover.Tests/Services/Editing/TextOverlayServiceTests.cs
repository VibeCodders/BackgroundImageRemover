using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services.Editing;

/// <summary>
/// Covers <see cref="TextOverlayService"/>: rendering a text watermark (with outline, shadow,
/// background plate, letter/line spacing, auto-fit and rotation) onto a BGR image.
/// </summary>
public class TextOverlayServiceTests
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

    // ------------------------------------------------------------------ Null/empty text

    [Fact]
    public void Render_NullText_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = TextOverlayService.Render(src, null, TextAnchor.Center, 24, new Vec3b(255, 255, 255), 1.0, 5);

        Assert.False(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_WhitespaceOnlyText_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = TextOverlayService.Render(src, "   ", TextAnchor.Center, 24, new Vec3b(255, 255, 255), 1.0, 5);

        Assert.False(AnyPixelDiffers(src, result));
    }

    // ------------------------------------------------------------------ Basic rendering

    [Fact]
    public void Render_BasicText_ChangesPixelsAndPreservesSizeAndType()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));

        using var result = TextOverlayService.Render(src, "Hi", TextAnchor.Center, 32, new Vec3b(255, 255, 255), 1.0, 10);

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_OpacityZero_ReturnsUnmodifiedClone()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));

        using var result = TextOverlayService.Render(src, "Hi", TextAnchor.Center, 32, new Vec3b(255, 255, 255), 0.0, 10);

        Assert.False(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_FontSizeBelowMinimum_IsClampedNotThrown()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));

        using var result = TextOverlayService.Render(src, "x", TextAnchor.Center, 0, new Vec3b(255, 255, 255), 1.0, 0);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void Render_TopLeftVsBottomRightAnchor_ProduceDifferentResults()
    {
        using var src = MakeBgr(200, 200, new Vec3b(0, 0, 0));

        using var topLeft = TextOverlayService.Render(src, "AB", TextAnchor.TopLeft, 24, new Vec3b(255, 255, 255), 1.0, 5);
        using var bottomRight = TextOverlayService.Render(src, "AB", TextAnchor.BottomRight, 24, new Vec3b(255, 255, 255), 1.0, 5);

        Assert.True(AnyPixelDiffers(topLeft, bottomRight));
    }

    // ------------------------------------------------------------------ Multi-line

    [Fact]
    public void Render_MultilineText_UnixAndWindowsNewlines_BothWork()
    {
        using var src = MakeBgr(300, 300, new Vec3b(0, 0, 0));

        using var unix = TextOverlayService.Render(src, "Line1\nLine2", TextAnchor.Center, 24, new Vec3b(255, 255, 255), 1.0, 5);
        using var windows = TextOverlayService.Render(src, "Line1\r\nLine2", TextAnchor.Center, 24, new Vec3b(255, 255, 255), 1.0, 5);

        Assert.True(AnyPixelDiffers(src, unix));
        Assert.False(AnyPixelDiffers(unix, windows));
    }

    // ------------------------------------------------------------------ TextOverlayOptions: styling

    [Fact]
    public void Render_Options_Bold_DiffersFromNonBold()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "Bold", Anchor = TextAnchor.Center, FontSize = 40 };

        using var normal = TextOverlayService.Render(src, baseOptions);
        using var bold = TextOverlayService.Render(src, baseOptions with { Bold = true });

        Assert.True(AnyPixelDiffers(normal, bold));
    }

    [Fact]
    public void Render_Options_OutlineThickness_ChangesOutput()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "O", Anchor = TextAnchor.Center, FontSize = 60, Color = new Vec3b(255, 255, 255) };

        using var noOutline = TextOverlayService.Render(src, baseOptions);
        using var outlined = TextOverlayService.Render(src, baseOptions with { OutlineThickness = 4, OutlineColor = new Vec3b(0, 0, 255) });

        Assert.True(AnyPixelDiffers(noOutline, outlined));
    }

    [Fact]
    public void Render_Options_ShadowOffset_ChangesOutput()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "S", Anchor = TextAnchor.Center, FontSize = 60, Color = new Vec3b(255, 255, 255) };

        using var noShadow = TextOverlayService.Render(src, baseOptions);
        using var shadow = TextOverlayService.Render(src, baseOptions with { ShadowOffset = 6, ShadowOpacity = 1.0, ShadowColor = new Vec3b(255, 0, 0) });

        Assert.True(AnyPixelDiffers(noShadow, shadow));
    }

    [Fact]
    public void Render_Options_ShadowBlur_DoesNotThrowAndChangesOutput()
    {
        using var src = MakeBgr(200, 100, new Vec3b(0, 0, 0));
        var options = new TextOverlayOptions
        {
            Text = "S", Anchor = TextAnchor.Center, FontSize = 60,
            ShadowOffset = 4, ShadowBlur = 3.0
        };

        using var result = TextOverlayService.Render(src, options);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_Options_BackgroundPlate_PaintsPlateBehindText()
    {
        using var src = MakeBgr(300, 200, new Vec3b(0, 0, 0));
        var options = new TextOverlayOptions
        {
            Text = "P", Anchor = TextAnchor.TopLeft, FontSize = 40, Margin = 0,
            Color = new Vec3b(255, 255, 255),
            BackgroundPlate = true,
            PlateColor = new Vec3b(0, 255, 0),
            PlateOpacity = 1.0,
            PlatePadding = 20
        };

        using var result = TextOverlayService.Render(src, options);

        // With TopLeft anchor and zero margin the block is placed at canvas (0,0). The plate
        // rect starts at (blockPad - platePad) = 4px in from the block edge, well before the
        // glyph strokes begin (around x = blockPad = 24), so (6,6) should be pure plate color.
        var px = result.Get<Vec3b>(6, 6);
        Assert.Equal(new Vec3b(0, 255, 0), px);
    }

    [Fact]
    public void Render_Options_LetterSpacing_WidensRenderedBlock()
    {
        using var src = MakeBgr(400, 200, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "WWWW", Anchor = TextAnchor.TopLeft, FontSize = 30, Margin = 0 };

        using var tight = TextOverlayService.Render(src, baseOptions);
        using var spaced = TextOverlayService.Render(src, baseOptions with { LetterSpacing = 20 });

        Assert.True(AnyPixelDiffers(tight, spaced));
    }

    [Fact]
    public void Render_Options_LineSpacing_ChangesMultilineLayout()
    {
        using var src = MakeBgr(300, 300, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "AB\nCD", Anchor = TextAnchor.TopLeft, FontSize = 30, Margin = 0 };

        using var tight = TextOverlayService.Render(src, baseOptions);
        using var spaced = TextOverlayService.Render(src, baseOptions with { LineSpacing = 30 });

        Assert.True(AnyPixelDiffers(tight, spaced));
    }

    [Fact]
    public void Render_Options_AutoFitWidth_ShrinksLongTextToFit()
    {
        using var src = MakeBgr(150, 100, new Vec3b(0, 0, 0));
        var longText = "This is a very long watermark text that would overflow";
        var noFit = new TextOverlayOptions { Text = longText, Anchor = TextAnchor.Center, FontSize = 40, AutoFitWidth = false };
        var withFit = new TextOverlayOptions { Text = longText, Anchor = TextAnchor.Center, FontSize = 40, AutoFitWidth = true };

        using var notFitted = TextOverlayService.Render(src, noFit);
        using var fitted = TextOverlayService.Render(src, withFit);

        // Both must produce a same-size, valid image without throwing regardless of overflow.
        Assert.Equal(src.Size(), notFitted.Size());
        Assert.Equal(src.Size(), fitted.Size());
        Assert.True(AnyPixelDiffers(notFitted, fitted));
    }

    [Fact]
    public void Render_Options_Rotation_ChangesOutputComparedToUnrotated()
    {
        using var src = MakeBgr(300, 300, new Vec3b(0, 0, 0));
        var baseOptions = new TextOverlayOptions { Text = "Rot", Anchor = TextAnchor.Center, FontSize = 40, Color = new Vec3b(255, 255, 255) };

        using var unrotated = TextOverlayService.Render(src, baseOptions);
        using var rotated = TextOverlayService.Render(src, baseOptions with { Rotation = 30.0 });

        Assert.Equal(src.Size(), rotated.Size());
        Assert.True(AnyPixelDiffers(unrotated, rotated));
    }

    [Fact]
    public void Render_Options_WideTextNearImageEdge_DoesNotThrowAndStaysInBounds()
    {
        // A large font on a small image with a large negative-position anchor scenario:
        // ensures the block-cropping fix in CompositeTextBlock doesn't crash or misplace when
        // the rendered text block is wider than the destination image.
        using var src = MakeBgr(40, 40, new Vec3b(0, 0, 0));
        var options = new TextOverlayOptions { Text = "WIDE TEXT BLOCK", Anchor = TextAnchor.BottomRight, FontSize = 60, Margin = 0 };

        using var result = TextOverlayService.Render(src, options);

        Assert.Equal(src.Size(), result.Size());
        Assert.True(AnyPixelDiffers(src, result));
    }

    [Fact]
    public void Render_Options_MarginNegative_TreatedAsZero()
    {
        using var src = MakeBgr(100, 100, new Vec3b(0, 0, 0));
        var options = new TextOverlayOptions { Text = "M", Anchor = TextAnchor.TopLeft, FontSize = 30, Margin = -20 };

        using var result = TextOverlayService.Render(src, options);

        Assert.Equal(src.Size(), result.Size());
    }
}
