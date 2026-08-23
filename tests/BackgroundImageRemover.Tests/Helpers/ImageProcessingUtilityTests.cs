using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Helpers;

public class ImageProcessingUtilityTests
{
    [Fact]
    public void AutoWhiteBalance_NeutralGray_IsUnchanged()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(120, 120, 120));

        using var result = ImageProcessingUtility.AutoWhiteBalance(src);

        var px = result.At<Vec3b>(5, 5);
        Assert.Equal(120, px.Item0);
        Assert.Equal(120, px.Item1);
        Assert.Equal(120, px.Item2);
    }

    [Fact]
    public void AutoWhiteBalance_NeutralizesColorCast()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(200, 100, 100)); // blue cast

        using var result = ImageProcessingUtility.AutoWhiteBalance(src);

        var px = result.At<Vec3b>(5, 5);
        Assert.True(px.Item0 < 200); // blue reduced
        Assert.True(px.Item2 > 100); // red lifted
        Assert.True(Math.Abs(px.Item0 - px.Item2) < Math.Abs(200 - 100)); // channels pulled together
    }

    [Fact]
    public void AutoWhiteBalance_UniformBlack_DoesNotCrash()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));

        using var result = ImageProcessingUtility.AutoWhiteBalance(src);

        Assert.Equal(0, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void AutoWhiteBalance_IsSharedByAllCallers()
    {
        // Guards the single-source-of-truth refactor: all three callers must agree exactly.
        using var src = new Mat(12, 12, MatType.CV_8UC3, new Scalar(180, 110, 90));

        using var utility = ImageProcessingUtility.AutoWhiteBalance(src);
        using var retouch = RetouchEffectsService.AutoWhiteBalance(src);
        using var levels = LevelsService.AutoWhiteBalance(src);

        AssertPixelsEqual(utility, retouch);
        AssertPixelsEqual(utility, levels);
    }

    [Fact]
    public void ApplyClahe_DefaultAndExplicitParameters_Match()
    {
        using var src = new Mat(24, 24, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(8, 8, 8, 8)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var a = ImageProcessingUtility.ApplyClahe(src);
        using var b = ImageProcessingUtility.ApplyClahe(src, clipLimit: 2.0, tileSize: 8);

        AssertPixelsEqual(a, b);
    }

    [Fact]
    public void ApplyClahe_IncreasesContrast_AndPreservesSizeAndType()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(110, 110, 110));
        using (var dark = new Mat(src, new Rect(5, 5, 20, 20)))
        {
            dark.SetTo(new Scalar(95, 95, 95));
        }

        using var result = ImageProcessingUtility.ApplyClahe(src);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
        ServiceTestHelper.AssertChangesPixels(src, result);
    }

    [Fact]
    public void AutoContrast_DelegatesToApplyClahe()
    {
        using var src = new Mat(24, 24, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(8, 8, 8, 8)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var autoContrast = RetouchEffectsService.AutoContrast(src);
        using var clahe = ImageProcessingUtility.ApplyClahe(src);

        AssertPixelsEqual(autoContrast, clahe);
    }

    [Fact]
    public void LevelsEqualize_DelegatesToApplyClahe()
    {
        using var src = new Mat(24, 24, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using (var bright = new Mat(src, new Rect(8, 8, 8, 8)))
        {
            bright.SetTo(new Scalar(200, 200, 200));
        }

        using var equalize = LevelsService.Equalize(src, clipLimit: 2.0, tileSize: 8);
        using var clahe = ImageProcessingUtility.ApplyClahe(src, clipLimit: 2.0, tileSize: 8);

        AssertPixelsEqual(equalize, clahe);
    }

    private static void AssertPixelsEqual(Mat a, Mat b)
    {
        Assert.Equal(a.Size(), b.Size());
        Assert.Equal(a.Type(), b.Type());
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        Cv2.MinMaxLoc(diff, out _, out double max);
        Assert.Equal(0, max);
    }
}
