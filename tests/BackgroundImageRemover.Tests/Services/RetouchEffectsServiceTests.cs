using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class RetouchEffectsServiceTests
{
    [Fact]
    public void Dehaze_PreservesSizeAndType()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(10, 10, 10, 10)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var result = RetouchEffectsService.Dehaze(src, 0.8);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void Defringe_PreservesSizeAndType()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(120, 90, 60));
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));

        using var result = RetouchEffectsService.Defringe(bgr, alpha);

        ServiceTestHelper.AssertPreservesSizeAndType(bgr, result);
    }

    [Fact]
    public void BlurBackground_FullAlpha_KeepsOriginalSharp()
    {
        using var bgr = new Mat(11, 11, MatType.CV_8UC3, Scalar.All(0));
        bgr.Set(5, 5, new Vec3b(255, 255, 255));
        using var alpha = new Mat(11, 11, MatType.CV_8UC1, Scalar.All(255));

        using var result = RetouchEffectsService.BlurBackground(bgr, alpha, 5);

        Assert.Equal(0, result.At<Vec3b>(5, 4).Item0); // neighbor stays dark (subject is "sharp")
        Assert.Equal(255, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void BlurBackground_ZeroAlpha_BlursWholeImage()
    {
        using var bgr = new Mat(11, 11, MatType.CV_8UC3, Scalar.All(0));
        bgr.Set(5, 5, new Vec3b(255, 255, 255));
        using var alpha = new Mat(11, 11, MatType.CV_8UC1, Scalar.All(0));

        using var result = RetouchEffectsService.BlurBackground(bgr, alpha, 5);

        Assert.True(result.At<Vec3b>(5, 4).Item0 > 0); // blur spread energy to the neighbor
    }

    [Fact]
    public void SharpenSubject_FullAlpha_LeavesFlatColorUnchanged()
    {
        using var bgr = new Mat(21, 21, MatType.CV_8UC3, new Scalar(120, 120, 120));
        using var alpha = new Mat(21, 21, MatType.CV_8UC1, Scalar.All(255));

        using var result = RetouchEffectsService.SharpenSubject(bgr, alpha, 2.0);

        // Sharpening a flat, uniform image has no edge to amplify.
        Assert.Equal(120, result.At<Vec3b>(10, 10).Item0);
    }

    [Fact]
    public void ColorBoost_FullAlpha_IncreasesColorSpread()
    {
        using var bgr = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200));
        using var alpha = new Mat(5, 5, MatType.CV_8UC1, Scalar.All(255));

        using var result = RetouchEffectsService.ColorBoost(bgr, alpha, 0.8);

        var before = bgr.At<Vec3b>(0, 0);
        var after = result.At<Vec3b>(0, 0);
        int beforeSpread = Math.Max(before.Item0, Math.Max(before.Item1, before.Item2)) - Math.Min(before.Item0, Math.Min(before.Item1, before.Item2));
        int afterSpread = Math.Max(after.Item0, Math.Max(after.Item1, after.Item2)) - Math.Min(after.Item0, Math.Min(after.Item1, after.Item2));

        Assert.True(afterSpread > beforeSpread);
    }

    [Fact]
    public void RemoveDust_PreservesUniformColor()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = RetouchEffectsService.RemoveDust(bgr, 3);

        Assert.Equal(100, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void SurfaceBlur_ZeroStrength_ReturnsClone()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 120, 140));

        using var result = RetouchEffectsService.SurfaceBlur(bgr, 0);

        Assert.Equal(bgr.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void AutoContrast_PreservesSizeAndType()
    {
        using var bgr = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));

        using var result = RetouchEffectsService.AutoContrast(bgr);

        ServiceTestHelper.AssertPreservesSizeAndType(bgr, result);
    }

    [Fact]
    public void AutoWhiteBalance_NeutralizesColorCast()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 100, 100)); // blue cast

        using var result = RetouchEffectsService.AutoWhiteBalance(bgr);

        var px = result.At<Vec3b>(5, 5);
        Assert.True(px.Item0 < 200); // blue reduced
        Assert.True(px.Item2 > 100); // red lifted
    }

    [Fact]
    public void ChromaticAberration_ZeroStrength_ReturnsClone()
    {
        using var bgr = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = RetouchEffectsService.ChromaticAberration(bgr, 0);

        Assert.Equal(bgr.At<Vec3b>(10, 10), result.At<Vec3b>(10, 10));
    }
}
