using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Regression tests for the Hue/Sat tool. These pin the in-place HSV adjustment behavior:
/// the service used to split the HSV channels, adjust the Mat in place, and then merge the
/// ORIGINAL unmodified channels back over it — silently discarding every adjustment. Any
/// regression that re-introduces that clobber fails these tests.
/// </summary>
public class HueSatServiceTests
{
    [Fact]
    public void AdjustHueSat_HueShift_ChangesHue()
    {
        // Pure blue in BGR (B=255, G=0, R=0) has HSV hue 120; shifting by +60 wraps to 0 (red).
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(255, 0, 0));

        using var result = HueSatService.AdjustHueSat(src, hueShift: 60, satMult: 1.0, valMult: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.True(px.Item2 > px.Item0, $"Expected red-dominant pixel after hue shift, got B={px.Item0} G={px.Item1} R={px.Item2}");
        Assert.Equal(255, px.Item2);
        Assert.Equal(0, px.Item0);
    }

    [Fact]
    public void AdjustHueSat_SaturationBoost_IncreasesColorSpread()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200));

        using var result = HueSatService.AdjustHueSat(src, hueShift: 0, satMult: 2.0, valMult: 1.0);

        var before = src.At<Vec3b>(0, 0);
        var after = result.At<Vec3b>(0, 0);
        int beforeSpread = Max(before) - Min(before);
        int afterSpread = Max(after) - Min(after);

        Assert.True(afterSpread > beforeSpread,
            $"Expected saturation boost to widen the channel spread ({beforeSpread} -> {afterSpread}).");

        static int Max(Vec3b v) => Math.Max(v.Item0, Math.Max(v.Item1, v.Item2));
        static int Min(Vec3b v) => Math.Min(v.Item0, Math.Min(v.Item1, v.Item2));
    }

    [Fact]
    public void AdjustHueSat_ValueMultiplier_BrightensAllChannels()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200));

        using var result = HueSatService.AdjustHueSat(src, hueShift: 0, satMult: 1.0, valMult: 1.5);

        var before = src.At<Vec3b>(0, 0);
        var after = result.At<Vec3b>(0, 0);
        Assert.True(after.Item0 > before.Item0);
        Assert.True(after.Item1 > before.Item1);
        Assert.True(after.Item2 > before.Item2);
    }

    [Fact]
    public void AdjustHueSat_IdentityParameters_LeaveImageNearlyUnchanged()
    {
        using var src = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200));

        using var result = HueSatService.AdjustHueSat(src, hueShift: 0, satMult: 1.0, valMult: 1.0);

        // BGR -> HSV -> BGR round trip is at worst off-by-one per channel.
        using var diff = new Mat();
        Cv2.Absdiff(src, result, diff);
        Cv2.MinMaxLoc(diff, out _, out double max);
        Assert.True(max <= 2, $"Identity adjustment changed pixels by up to {max}.");
    }

    [Fact]
    public void AdjustHueSatRegion_OnlyModifiesMaskedPixels()
    {
        using var src = new Mat(4, 4, MatType.CV_8UC3, new Scalar(255, 0, 0)); // blue
        using var mask = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        using (var left = new Mat(mask, new Rect(0, 0, 2, 4)))
        {
            left.SetTo(new Scalar(255));
        }

        using var result = HueSatService.AdjustHueSatRegion(src, mask, hueShift: 60, satMult: 1.0, valMult: 1.0);

        var masked = result.At<Vec3b>(2, 1);  // inside the mask -> shifted toward red
        var unmasked = result.At<Vec3b>(2, 3); // outside the mask -> untouched
        Assert.True(masked.Item2 > masked.Item0);
        Assert.Equal(255, unmasked.Item0);
        Assert.Equal(0, unmasked.Item2);
    }

    [Fact]
    public void AdjustHueSat_NullImage_ReturnsEmptyMat()
    {
        using var result = HueSatService.AdjustHueSat(null!, hueShift: 30, satMult: 1.0, valMult: 1.0);

        Assert.True(result.Empty());
    }

    [Fact]
    public void AdjustHueSat_ExtremeValues_DoesNotCrash()
    {
        using var src = new Mat(8, 8, MatType.CV_8UC3, new Scalar(40, 90, 200));

        using var result = HueSatService.AdjustHueSat(src, hueShift: 5000, satMult: 20.0, valMult: 20.0);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void AdjustHueSatRegion_MaskLargerThanImage_DoesNotCrash()
    {
        using var src = new Mat(3, 3, MatType.CV_8UC3, new Scalar(255, 0, 0));
        using var mask = new Mat(3, 3, MatType.CV_8UC1, Scalar.All(255));

        using var result = HueSatService.AdjustHueSatRegion(src, mask, hueShift: 60, satMult: 1.0, valMult: 1.0);

        Assert.True(result.At<Vec3b>(1, 1).Item2 > result.At<Vec3b>(1, 1).Item0);
    }
}
