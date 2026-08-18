using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

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

        Assert.Equal(src.Size(), result.Size());
        Assert.Equal(src.Type(), result.Type());
    }

    [Fact]
    public void Defringe_PreservesSizeAndType()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(120, 90, 60));
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));

        using var result = RetouchEffectsService.Defringe(bgr, alpha);

        Assert.Equal(bgr.Size(), result.Size());
        Assert.Equal(bgr.Type(), result.Type());
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
}
