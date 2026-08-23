using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class BlurServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ BlurRegion

    [Fact]
    public void BlurRegion_MaskAllZero_LeavesImageUntouched()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(0));

        using var result = BlurService.BlurRegion(input, mask, 5.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void BlurRegion_MaskAllWhite_EqualsBlurAll()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var region = BlurService.BlurRegion(input, mask, 5.0);
        using var all = BlurService.BlurAll(input, 5.0);

        AssertNoChange(region, all);
    }

    [Fact]
    public void BlurRegion_PartialMask_ChangesOnlyMaskedArea()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 15, 15, 10, 10);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Rect(0, 0, 20, 40), Scalar.All(255), -1);

        using var result = BlurService.BlurRegion(input, mask, 5.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);

        // Outside the mask (mask value 0), the blend formula leaves the original pixel exactly
        // untouched: the mask itself has a hard edge, so nothing beyond column 19 can change.
        for (int x = 20; x < 40; x++)
        {
            Assert.Equal(input.Get<Vec3b>(20, x), result.Get<Vec3b>(20, x));
        }
    }

    [Fact]
    public void BlurRegion_BlurBleedsAcrossColorEdgesWithinTheMaskedRegion()
    {
        // The Gaussian blur is computed over the whole image (not clipped to the mask) before
        // masking is applied, so a masked background pixel that sits right next to the
        // rectangle picks up some of the rectangle's color - the "transition is naturally
        // soft" behavior documented on BlurService.BlurRegion, rather than a flat box blur
        // confined to the painted region.
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 15, 15, 10, 10);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var result = BlurService.BlurRegion(input, mask, 5.0);

        // (14, 20): one column to the left of the rectangle (which starts at column 15), still
        // background-colored in the input, but close enough to be reached by the sigma-5 kernel.
        var original = input.Get<Vec3b>(20, 14);
        var blended = result.Get<Vec3b>(20, 14);
        Assert.Equal(new Vec3b(10, 20, 30), original);
        Assert.NotEqual(original, blended);
        // Every channel should have shifted toward the rectangle's (220, 220, 220) color.
        Assert.True(blended.Item0 > original.Item0);
        Assert.True(blended.Item1 > original.Item1);
        Assert.True(blended.Item2 > original.Item2);
    }

    [Fact]
    public void BlurRegion_RadiusClampedToMinimum()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var negative = BlurService.BlurRegion(input, mask, -5.0);
        using var small = BlurService.BlurRegion(input, mask, 0.1);
        using var half = BlurService.BlurRegion(input, mask, 0.5);

        AssertNoChange(negative, half);
        AssertNoChange(small, half);
    }

    // ------------------------------------------------------------------ BlurAll

    [Fact]
    public void BlurAll_UniformImage_LeavesColorUnchanged()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = BlurService.BlurAll(input, 5.0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void BlurAll_ImageWithEdge_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = BlurService.BlurAll(input, 5.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void BlurAll_LargerRadius_ProducesMoreBlur()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 15, 15, 10, 10);

        using var small = BlurService.BlurAll(input, 1.0);
        using var large = BlurService.BlurAll(input, 10.0);

        AssertResultsDiffer(small, large);
    }

    [Fact]
    public void BlurAll_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = BlurService.BlurAll(input, 5.0);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ MotionBlur

    [Fact]
    public void MotionBlur_UniformImage_PreservesAverageColorRegardlessOfAngle()
    {
        // A properly-normalized motion-blur kernel must sum to 1, so blurring a flat-color
        // image at any angle should leave the color unchanged (up to integer rounding).
        using var input = MakeUniform(30, 30, new Scalar(64, 128, 192));

        foreach (double angle in new[] { 0.0, 30.0, 45.0, 60.0, 90.0, 135.0, 200.0 })
        {
            using var result = BlurService.MotionBlur(input, 15.0, angle);
            var expected = input.Get<Vec3b>(15, 15);
            var actual = result.Get<Vec3b>(15, 15);
            Assert.InRange(Math.Abs(expected.Item0 - actual.Item0), 0, 2);
            Assert.InRange(Math.Abs(expected.Item1 - actual.Item1), 0, 2);
            Assert.InRange(Math.Abs(expected.Item2 - actual.Item2), 0, 2);
        }
    }

    [Fact]
    public void MotionBlur_PreservesSizeAndType()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = BlurService.MotionBlur(input, 7.0, 45.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void MotionBlur_ImageWithEdge_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = BlurService.MotionBlur(input, 9.0, 0.0);

        AssertChangesPixels(input, result);
    }

    [Fact]
    public void MotionBlur_DifferentAngles_ProduceDifferentResults()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var horizontal = BlurService.MotionBlur(input, 9.0, 0.0);
        using var vertical = BlurService.MotionBlur(input, 9.0, 90.0);

        AssertResultsDiffer(horizontal, vertical);
    }

    [Fact]
    public void MotionBlur_LengthClampedToMinimumOfOne()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var zero = BlurService.MotionBlur(input, 0.0, 0.0);
        using var negative = BlurService.MotionBlur(input, -5.0, 0.0);
        using var one = BlurService.MotionBlur(input, 1.0, 0.0);

        AssertNoChange(zero, one);
        AssertNoChange(negative, one);
    }

    [Fact]
    public void MotionBlur_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = BlurService.MotionBlur(input, 9.0, 45.0);

        AssertPreservesSizeAndType(input, result);
    }
}
