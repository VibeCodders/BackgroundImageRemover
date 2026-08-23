using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Tests.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the standalone Vignette tool (distinct from FrameService.AddVignette).</summary>
public class VignetteServiceTests
{
    private static Mat MakeUniform(int width = 100, int height = 100, byte value = 128)
        => new(height, width, MatType.CV_8UC3, new Scalar(value, value, value));

    [Fact]
    public void Apply_ZeroStrength_ReturnsClone()
    {
        using var image = MakeUniform();

        using var result = VignetteService.Apply(image, 0.0);

        ServiceTestHelper.AssertNoChange(image, result);
    }

    [Fact]
    public void Apply_DarkensCorners_KeepsCenter()
    {
        using var image = MakeUniform();

        using var result = VignetteService.Apply(image, 0.8, roundness: 0.5, feather: 0.5);

        ServiceTestHelper.AssertPreservesSizeAndType(image, result);
        Assert.True(result.Get<Vec3b>(2, 2).Item0 < image.Get<Vec3b>(2, 2).Item0);   // corner darkened
        Assert.True(result.Get<Vec3b>(50, 50).Item0 >= image.Get<Vec3b>(50, 50).Item0 - 1); // center kept
    }

    [Fact]
    public void Apply_Invert_LightensCorners()
    {
        using var image = MakeUniform(value: 100);

        using var result = VignetteService.Apply(image, 0.8, invert: true);

        Assert.True(result.Get<Vec3b>(2, 2).Item0 > image.Get<Vec3b>(2, 2).Item0);
    }

    [Fact]
    public void Apply_OneByOneImage_DoesNotCrash()
    {
        using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(128, 128, 128));

        using var result = VignetteService.Apply(image, 0.8);

        Assert.Equal(new Size(1, 1), result.Size());
    }
}
