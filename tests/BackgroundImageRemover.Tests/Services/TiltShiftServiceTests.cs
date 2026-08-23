using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Tests.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the Tilt-shift (miniature) tool.</summary>
public class TiltShiftServiceTests
{
    private static Mat MakeGradient(int width = 80, int height = 80)
    {
        var image = new Mat(height, width, MatType.CV_8UC3);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image.Set<Vec3b>(y, x, new Vec3b((byte)x, (byte)y, 100));
            }
        }

        return image;
    }

    [Fact]
    public void Apply_NoBlurAndNoSaturationBoost_ReturnsClone()
    {
        using var image = MakeGradient();

        using var result = TiltShiftService.Apply(image, 0.5, 0.3, blurRadius: 0, vertical: false, saturationBoost: 0);

        ServiceTestHelper.AssertNoChange(image, result);
    }

    [Fact]
    public void Apply_HorizontalBand_BlursTopAndBottom_KeepsCenterSharp()
    {
        using var image = MakeGradient();

        // Narrow band centered vertically (focusCenter 0.5), heavy blur, no saturation boost.
        using var result = TiltShiftService.Apply(image, 0.5, 0.2, blurRadius: 8, vertical: false, saturationBoost: 0);

        // The top row is far outside the band: blurred away from the gradient -> changed.
        Assert.NotEqual(image.Get<Vec3b>(2, 40), result.Get<Vec3b>(2, 40));
        // The band center row stays close to the original.
        var bandRow = result.Get<Vec3b>(40, 40);
        var origRow = image.Get<Vec3b>(40, 40);
        Assert.True(Math.Abs(bandRow.Item0 - origRow.Item0) <= 10);
        Assert.Equal(image.Size(), result.Size());
    }

    [Fact]
    public void Apply_VerticalBand_BlursLeftAndRight()
    {
        using var image = MakeGradient();

        using var result = TiltShiftService.Apply(image, 0.5, 0.2, blurRadius: 8, vertical: true, saturationBoost: 0);

        // Column 2 is outside the vertical band: changed by the blur.
        Assert.NotEqual(image.Get<Vec3b>(40, 2), result.Get<Vec3b>(40, 2));
        // Column 40 is inside the band: stays close.
        var bandCol = result.Get<Vec3b>(40, 40);
        var origCol = image.Get<Vec3b>(40, 40);
        Assert.True(Math.Abs(bandCol.Item0 - origCol.Item0) <= 10);
    }

    [Fact]
    public void Apply_SaturationBoost_ChangesColorsEvenWithoutBlur()
    {
        using var image = MakeGradient();

        using var result = TiltShiftService.Apply(image, 0.5, 0.3, blurRadius: 0, vertical: false, saturationBoost: 0.5);

        ServiceTestHelper.AssertChangesPixels(image, result);
    }

    [Fact]
    public void Apply_OneByOneImage_DoesNotCrash()
    {
        using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = TiltShiftService.Apply(image, 0.5, 0.3, blurRadius: 8, vertical: false, saturationBoost: 0.5);

        Assert.Equal(new Size(1, 1), result.Size());
    }
}
