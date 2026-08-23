using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Regression tests for the region-restricted dodge/burn. The region overload used to blend the
/// adjusted image with ITSELF, so the painted mask was silently ignored and the whole image was
/// affected — identical to the global overload.
/// </summary>
public class DodgeBurnServiceTests
{
    [Fact]
    public void DodgeBurnRegion_OnlyAffectsMaskedPixels()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        using (var left = new Mat(mask, new Rect(0, 0, 5, 10)))
        {
            left.SetTo(new Scalar(255));
        }

        using var result = DodgeBurnService.DodgeBurnRegion(bgr, mask, dodge: true, strength: 0.5);

        Assert.True(result.At<Vec3b>(5, 2).Item0 > 200); // masked: dodged (brightened)
        Assert.Equal(200, result.At<Vec3b>(5, 7).Item0);  // unmasked: untouched
    }

    [Fact]
    public void DodgeBurnRegion_Burn_DarkensOnlyMaskedPixels()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        using (var left = new Mat(mask, new Rect(0, 0, 5, 10)))
        {
            left.SetTo(new Scalar(255));
        }

        using var result = DodgeBurnService.DodgeBurnRegion(bgr, mask, dodge: false, strength: 0.5);

        Assert.True(result.At<Vec3b>(5, 2).Item0 < 200); // masked: burned (darkened)
        Assert.Equal(200, result.At<Vec3b>(5, 7).Item0);  // unmasked: untouched
    }

    [Fact]
    public void DodgeBurnRegion_ZeroStrength_LeavesImageUnchanged()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(150, 150, 150));
        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));

        using var result = DodgeBurnService.DodgeBurnRegion(bgr, mask, dodge: true, strength: 0.0);

        ServiceTestHelper.AssertNoChange(bgr, result);
    }

    [Fact]
    public void DodgeBurnRegion_DiffersFromGlobalOverload()
    {
        // With a partial mask the region overload must NOT equal the global one.
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        using (var left = new Mat(mask, new Rect(0, 0, 5, 10)))
        {
            left.SetTo(new Scalar(255));
        }

        using var region = DodgeBurnService.DodgeBurnRegion(bgr, mask, dodge: true, strength: 0.5);
        using var global = DodgeBurnService.DodgeBurnAll(bgr, dodge: true, strength: 0.5);

        ServiceTestHelper.AssertResultsDiffer(region, global);
        Assert.Equal(200, region.At<Vec3b>(5, 7).Item0);
        Assert.True(global.At<Vec3b>(5, 7).Item0 > 200);
    }

    [Fact]
    public void DodgeBurnAll_AffectsWholeImage()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 200, 200));

        using var result = DodgeBurnService.DodgeBurnAll(bgr, dodge: true, strength: 0.5);

        Assert.True(result.At<Vec3b>(5, 5).Item0 > 200);
        Assert.True(result.At<Vec3b>(0, 0).Item0 > 200);
    }
}
