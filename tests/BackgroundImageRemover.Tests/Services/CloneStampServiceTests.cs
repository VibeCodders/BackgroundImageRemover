using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Regression tests for the clone-stamp brush behavior. The service used to sample its own
/// brush mask at the center pixel — always alpha 1.0 — so the mask's softness/hardness was
/// discarded and every stamped pixel got a hard-edged full-opacity blend. The mask's own
/// intensity must drive the blend instead.
/// </summary>
public class CloneStampServiceTests
{
    [Fact]
    public void CloneStamp_BinaryMask_CopiesSourceAtFullOpacity()
    {
        // Gray canvas with a white square at (1,1); stamp it onto (5,5) via a (4,4) source offset.
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using (var white = new Mat(bgr, new Rect(1, 1, 2, 2)))
        {
            white.SetTo(new Scalar(255, 255, 255));
        }

        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        mask.Set(5, 5, 255);

        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(4, 4), opacity: 1.0);

        var stamped = result.At<Vec3b>(5, 5);
        Assert.Equal(255, stamped.Item0);
        Assert.Equal(255, stamped.Item1);
        Assert.Equal(255, stamped.Item2);

        // Neighbors outside the mask stay untouched.
        Assert.Equal(100, result.At<Vec3b>(5, 4).Item0);
        Assert.Equal(100, result.At<Vec3b>(4, 5).Item0);
    }

    [Fact]
    public void CloneStamp_GradientMask_FeathersTheBlend()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using (var white = new Mat(bgr, new Rect(1, 1, 2, 2)))
        {
            white.SetTo(new Scalar(255, 255, 255));
        }

        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        mask.Set(5, 5, 128); // half intensity -> roughly half-way blend

        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(4, 4), opacity: 1.0);

        var px = result.At<Vec3b>(5, 5).Item0;
        Assert.InRange(px, 150, 200); // strictly between untouched (100) and full copy (255)
    }

    [Fact]
    public void CloneStamp_HalfOpacity_BlendsByOpacity()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using (var white = new Mat(bgr, new Rect(1, 1, 2, 2)))
        {
            white.SetTo(new Scalar(255, 255, 255));
        }

        using var mask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
        mask.Set(5, 5, 255);

        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(4, 4), opacity: 0.5);

        var px = result.At<Vec3b>(5, 5).Item0;
        Assert.InRange(px, 150, 200); // 100 + (255-100) * 0.5 = 177.5
    }

    [Fact]
    public void CloneStamp_OutOfBoundsSource_SkipsPixelWithoutCrashing()
    {
        using var bgr = new Mat(8, 8, MatType.CV_8UC3, new Scalar(90, 90, 90));
        using var mask = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(255));

        // The only valid source for the top-left pixel would be at (-100, -100): nothing to copy.
        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(100, 100), opacity: 1.0);

        ServiceTestHelper.AssertPreservesSizeAndType(bgr, result);
        Assert.Equal(90, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void CloneStamp_ZeroOpacity_LeavesImageUnchanged()
    {
        using var bgr = new Mat(8, 8, MatType.CV_8UC3, new Scalar(120, 130, 140));
        using var mask = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(255));

        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(2, 2), opacity: 0.0);

        ServiceTestHelper.AssertNoChange(bgr, result);
    }

    [Fact]
    public void CloneStamp_EmptyMask_ReturnsClone()
    {
        using var bgr = new Mat(6, 6, MatType.CV_8UC3, new Scalar(50, 60, 70));
        using var mask = new Mat(6, 6, MatType.CV_8UC1, Scalar.All(0));

        using var result = CloneStampService.CloneStamp(bgr, mask, new Point(1, 1), opacity: 1.0);

        ServiceTestHelper.AssertNoChange(bgr, result);
    }
}
