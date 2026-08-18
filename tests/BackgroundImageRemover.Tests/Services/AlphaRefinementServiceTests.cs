using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class AlphaRefinementServiceTests
{
    [Fact]
    public void Invert_FlipsOpaqueAndTransparent()
    {
        using var alpha = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        alpha.Set(1, 1, (byte)0);

        using var result = AlphaRefinementService.Invert(alpha);

        Assert.Equal(0, result.Get<byte>(0, 0));
        Assert.Equal(255, result.Get<byte>(1, 1));
    }

    [Fact]
    public void Feather_SpreadsOpacityBeyondTheEdge()
    {
        // Left half opaque, right half transparent.
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        using var left = new Mat(alpha, new Rect(0, 0, 5, 10));
        left.SetTo(Scalar.All(255));

        using var result = AlphaRefinementService.Feather(alpha, sigma: 2.0);

        // The column just past the hard edge is now partially opaque.
        byte spill = result.Get<byte>(5, 6);
        Assert.True(spill > 0 && spill < 255, $"expected feathered edge, got {spill}");
    }

    [Fact]
    public void Smooth_PreservesOpaqueInterior()
    {
        using var alpha = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(255));

        using var result = AlphaRefinementService.Smooth(alpha);

        Assert.Equal(255, result.Get<byte>(10, 10));
    }

    [Fact]
    public void RemoveSpecks_RemovesIsolatedForegroundPixel()
    {
        using var alpha = new Mat(11, 11, MatType.CV_8UC1, Scalar.All(255));
        alpha.Set(5, 5, (byte)0); // a single "background hole"

        using var result = AlphaRefinementService.RemoveSpecks(alpha);

        Assert.Equal(255, result.Get<byte>(5, 5)); // hole filled by the close operation
    }
}
