using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class SharpenServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ SharpenRegion

    [Fact]
    public void SharpenRegion_StrengthZero_ReturnsClone()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var result = SharpenService.SharpenRegion(input, mask, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void SharpenRegion_NegativeStrength_ClampedToZero_ReturnsClone()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var result = SharpenService.SharpenRegion(input, mask, -1.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void SharpenRegion_MaskAllZero_LeavesImageUntouched()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(0));

        using var result = SharpenService.SharpenRegion(input, mask, 1.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void SharpenRegion_MaskAllWhite_EqualsSharpenAll()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var region = SharpenService.SharpenRegion(input, mask, 0.8);
        using var all = SharpenService.SharpenAll(input, 0.8);

        AssertNoChange(region, all);
    }

    [Fact]
    public void SharpenRegion_PartialMask_ChangesOnlyMaskedArea()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 15, 15, 10, 10);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Rect(0, 0, 20, 40), Scalar.All(255), -1);

        using var result = SharpenService.SharpenRegion(input, mask, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);

        // Far from the masked region and the edge, pixels are untouched.
        Assert.Equal(input.Get<Vec3b>(0, 39), result.Get<Vec3b>(0, 39));
    }

    [Fact]
    public void SharpenRegion_StrengthAboveOne_ClampedToOne()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        using var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(255));

        using var overOne = SharpenService.SharpenRegion(input, mask, 5.0);
        using var one = SharpenService.SharpenRegion(input, mask, 1.0);

        AssertNoChange(overOne, one);
    }

    // ------------------------------------------------------------------ SharpenAll

    [Fact]
    public void SharpenAll_StrengthZero_ReturnsClone()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = SharpenService.SharpenAll(input, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void SharpenAll_UniformImage_LeavesColorUnchanged()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = SharpenService.SharpenAll(input, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void SharpenAll_ImageWithEdge_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = SharpenService.SharpenAll(input, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void SharpenAll_HigherStrength_ProducesStrongerEffect()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 15, 15, 10, 10);

        using var mild = SharpenService.SharpenAll(input, 0.2);
        using var strong = SharpenService.SharpenAll(input, 1.0);

        AssertResultsDiffer(mild, strong);
    }

    [Fact]
    public void SharpenAll_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = SharpenService.SharpenAll(input, 1.0);

        AssertPreservesSizeAndType(input, result);
    }
}
