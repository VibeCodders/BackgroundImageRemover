using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class BackgroundCompositingServiceTests
{
    [Fact]
    public void TrimTransparentBorders_CropsToNonTransparentBounds()
    {
        // 40x30 image, fully transparent except a 15x10 opaque rectangle at (8,5).
        using var bgra = new Mat(30, 40, MatType.CV_8UC4, Scalar.All(0));
        using var opaque = new Mat(bgra, new Rect(8, 5, 15, 10));
        opaque.SetTo(new Scalar(200, 100, 50, 255));

        using var trimmed = BackgroundCompositingService.TrimTransparentBorders(bgra);

        Assert.Equal(15, trimmed.Width);
        Assert.Equal(10, trimmed.Height);
        Assert.Equal(4, trimmed.Channels());

        var px = trimmed.At<Vec4b>(0, 0);
        Assert.Equal(200, px.Item0); // B
        Assert.Equal(100, px.Item1); // G
        Assert.Equal(50, px.Item2);  // R
        Assert.Equal(255, px.Item3); // A
    }

    [Fact]
    public void TrimTransparentBorders_ReturnsImageUnchanged_WhenFullyTransparent()
    {
        using var bgra = new Mat(10, 20, MatType.CV_8UC4, Scalar.All(0));

        using var trimmed = BackgroundCompositingService.TrimTransparentBorders(bgra);

        Assert.Equal(20, trimmed.Width);
        Assert.Equal(10, trimmed.Height);
    }

    [Fact]
    public void HasMeaningfulTransparency_ReturnsFalse_ForNullAlpha()
    {
        Assert.False(BackgroundCompositingService.HasMeaningfulTransparency(null));
    }

    [Fact]
    public void HasMeaningfulTransparency_ReturnsFalse_ForUniformlyOpaqueAlpha()
    {
        // A PNG can carry a 4th channel that is opaque everywhere -- a plain photo saved in an
        // RGBA container, not a real cutout. This must not be mistaken for a cutout.
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));

        Assert.False(BackgroundCompositingService.HasMeaningfulTransparency(alpha));
    }

    [Fact]
    public void HasMeaningfulTransparency_ReturnsTrue_WhenAnyPixelIsNotFullyOpaque()
    {
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));
        alpha.Set(5, 5, (byte)0);

        Assert.True(BackgroundCompositingService.HasMeaningfulTransparency(alpha));
    }

    [Fact]
    public void ZeroFullyTransparentPixels_ClearsColorOnlyWhereAlphaIsZero()
    {
        using var bgra = new Mat(4, 4, MatType.CV_8UC4, new Scalar(10, 20, 30, 255));

        // A fully transparent pixel with leftover (visually invisible) color data...
        bgra.Set(1, 1, new Vec4b(200, 150, 100, 0));
        // ...and a semi-transparent edge pixel, whose color must be preserved for blending.
        bgra.Set(2, 2, new Vec4b(60, 70, 80, 128));

        BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);

        var cleared = bgra.At<Vec4b>(1, 1);
        Assert.Equal(0, cleared.Item0);
        Assert.Equal(0, cleared.Item1);
        Assert.Equal(0, cleared.Item2);
        Assert.Equal(0, cleared.Item3);

        var edge = bgra.At<Vec4b>(2, 2);
        Assert.Equal(60, edge.Item0);
        Assert.Equal(70, edge.Item1);
        Assert.Equal(80, edge.Item2);
        Assert.Equal(128, edge.Item3);

        var untouched = bgra.At<Vec4b>(0, 0);
        Assert.Equal(10, untouched.Item0);
        Assert.Equal(20, untouched.Item1);
        Assert.Equal(30, untouched.Item2);
        Assert.Equal(255, untouched.Item3);
    }
}
