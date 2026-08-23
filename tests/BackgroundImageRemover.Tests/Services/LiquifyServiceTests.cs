using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Tests.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the Liquify warp tool (Pinch/Bloat/Twirl/Push modes).</summary>
public class LiquifyServiceTests
{
    /// <summary>Creates a gradient image so every pixel is distinguishable after a warp.</summary>
    private static Mat MakeGradient(int width = 60, int height = 60)
    {
        var image = new Mat(height, width, MatType.CV_8UC3);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image.Set<Vec3b>(y, x, new Vec3b((byte)x, (byte)y, (byte)((x + y) / 2)));
            }
        }

        return image;
    }

    [Theory]
    [InlineData(LiquifyMode.Pinch)]
    [InlineData(LiquifyMode.Bloat)]
    [InlineData(LiquifyMode.Twirl)]
    [InlineData(LiquifyMode.PushLeft)]
    [InlineData(LiquifyMode.PushRight)]
    [InlineData(LiquifyMode.PushUp)]
    [InlineData(LiquifyMode.PushDown)]
    public void Warp_EveryMode_ChangesPixelsInsideTheRegion(LiquifyMode mode)
    {
        using var image = MakeGradient();

        using var result = LiquifyService.Warp(image, new Point(30, 30), radius: 12, strength: 1.0, mode);

        ServiceTestHelper.AssertPreservesSizeAndType(image, result);
        ServiceTestHelper.AssertChangesPixels(image, result);
    }

    [Fact]
    public void Warp_PixelsFarOutsideTheRadius_AreUntouched()
    {
        using var image = MakeGradient();

        using var result = LiquifyService.Warp(image, new Point(30, 30), radius: 10, strength: 1.0, LiquifyMode.PushLeft);

        // The far corner is way outside the falloff: it must map back onto itself exactly.
        Assert.Equal(image.Get<Vec3b>(3, 3), result.Get<Vec3b>(3, 3));
    }

    [Fact]
    public void Warp_ZeroStrength_ReturnsClone()
    {
        using var image = MakeGradient();

        using var result = LiquifyService.Warp(image, new Point(30, 30), radius: 10, strength: 0.0, LiquifyMode.Twirl);

        ServiceTestHelper.AssertNoChange(image, result);
    }

    [Fact]
    public void Warp_OneByOneImage_DoesNotCrash()
    {
        using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = LiquifyService.Warp(image, new Point(0, 0), radius: 5, strength: 1.0, LiquifyMode.Pinch);

        Assert.Equal(new Size(1, 1), result.Size());
    }
}
