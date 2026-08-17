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
}
