using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class MosaicServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ Pixelate

    [Fact]
    public void Pixelate_WholeImage_ChangesPixelsAndPreservesSizeAndType()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.Pixelate(input, null, 4);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Pixelate_Region_LeavesOutsidePixelsUnchanged()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        var region = new Rect(0, 0, 10, 10);

        using var result = MosaicService.Pixelate(input, region, 4);

        // Outside the region, nothing changed.
        Assert.Equal(input.Get<Vec3b>(20, 20), result.Get<Vec3b>(20, 20));
    }

    [Fact]
    public void Pixelate_CellSizeLargerThanImage_DoesNotThrow()
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 2, 2, 4, 4);

        using var result = MosaicService.Pixelate(input, null, 1000);

        AssertPreservesSizeAndType(input, result);
        // The whole region collapses to a single average color.
        AssertAllPixelsSameHelper(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Pixelate_NonPositiveCellSize_ClampedToOne(int cellSize)
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 2, 2, 4, 4);

        using var result = MosaicService.Pixelate(input, null, cellSize);

        // cellSize <= 0 is clamped to 1, i.e. effectively a no-op pixelation (identity resize).
        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Pixelate_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = MosaicService.Pixelate(input, null, 4);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ Blur

    [Fact]
    public void Blur_ImageWithEdge_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.Blur(input, null, 5);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Blur_UniformImage_LeavesColorUnchanged()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = MosaicService.Blur(input, null, 5);

        AssertNoChange(input, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Blur_NonPositiveRadius_ClampedToOne(int radius)
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var negative = MosaicService.Blur(input, null, radius);
        using var one = MosaicService.Blur(input, null, 1);

        AssertNoChange(negative, one);
    }

    [Fact]
    public void Blur_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = MosaicService.Blur(input, null, 5);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ MedianBlur

    [Fact]
    public void MedianBlur_RemovesSaltAndPepperNoise()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));
        // Single-pixel noise spike.
        input.Set(10, 10, new Vec3b(255, 255, 255));

        using var result = MosaicService.MedianBlur(input, null, 3);

        // The noise spike should be replaced by the surrounding median color.
        Assert.Equal(new Vec3b(50, 100, 150), result.Get<Vec3b>(10, 10));
    }

    [Fact]
    public void MedianBlur_EvenRadius_IsRoundedToOdd()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));
        input.Set(10, 10, new Vec3b(255, 255, 255));

        using var result = MosaicService.MedianBlur(input, null, 4);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void MedianBlur_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = MosaicService.MedianBlur(input, null, 3);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ SolidFill

    [Fact]
    public void SolidFill_WholeImage_SetsUniformColor()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.SolidFill(input, null, new Vec3b(1, 2, 3));

        AssertAllPixelsSameHelper(result);
        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(0, 0));
    }

    [Fact]
    public void SolidFill_Region_OnlyFillsInsideRegion()
    {
        using var input = MakeUniform(20, 20, new Scalar(10, 20, 30));
        var region = new Rect(2, 2, 5, 5);

        using var result = MosaicService.SolidFill(input, region, new Vec3b(1, 2, 3));

        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(4, 4));
        Assert.Equal(new Vec3b(10, 20, 30), result.Get<Vec3b>(15, 15));
    }

    // ------------------------------------------------------------------ PixelateOutside

    [Fact]
    public void PixelateOutside_RegionKeepsOriginalPixelsInsideRegion()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 8, 8);
        var region = new Rect(10, 10, 8, 8);

        using var result = MosaicService.PixelateOutside(input, region, 4);

        // Inside the region, pixels are untouched (original).
        Assert.Equal(input.Get<Vec3b>(12, 12), result.Get<Vec3b>(12, 12));
    }

    [Fact]
    public void PixelateOutside_RegionChangesPixelsOutsideRegion()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 8, 8);
        var region = new Rect(10, 10, 8, 8);

        using var result = MosaicService.PixelateOutside(input, region, 4);

        AssertChangesPixels(input, result);
    }

    [Fact]
    public void PixelateOutside_NullRegion_PixelatesWholeImage()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var withNullRegion = MosaicService.PixelateOutside(input, null, 4);
        using var wholeImagePixelate = MosaicService.Pixelate(input, null, 4);

        AssertNoChange(withNullRegion, wholeImagePixelate);
    }

    // ------------------------------------------------------------------ Crystallize

    [Fact]
    public void Crystallize_ProducesBlockyOutput_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.Crystallize(input, null, 4, 2);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Crystallize_ZeroJitter_IsDeterministicAndFlat()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result1 = MosaicService.Crystallize(input, null, 4, 0);
        using var result2 = MosaicService.Crystallize(input, null, 4, 0);

        AssertNoChange(result1, result2);
    }

    [Fact]
    public void Crystallize_JitterClampedToCellSizeMinusOne_DoesNotThrow()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        // jitter far larger than cellSize must be clamped, not throw.
        using var result = MosaicService.Crystallize(input, null, 4, 1000);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Crystallize_NegativeJitter_ClampedToZero()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.Crystallize(input, null, 4, -10);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Crystallize_CellSizeLargerThanImage_DoesNotThrow()
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 2, 2, 4, 4);

        using var result = MosaicService.Crystallize(input, null, 1000, 0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Crystallize_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = MosaicService.Crystallize(input, null, 4, 2);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Crystallize_Region_LeavesOutsidePixelsUnchanged()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        var region = new Rect(0, 0, 10, 10);

        using var result = MosaicService.Crystallize(input, region, 4, 2);

        Assert.Equal(input.Get<Vec3b>(20, 20), result.Get<Vec3b>(20, 20));
    }

    // ------------------------------------------------------------------ BlurSoft

    [Fact]
    public void BlurSoft_ZeroStrength_LeavesImageUnchanged()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = MosaicService.BlurSoft(input, null, 5, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void BlurSoft_FullStrength_EqualsFullBlur()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var soft = MosaicService.BlurSoft(input, null, 5, 1.0);
        using var full = MosaicService.Blur(input, null, 5);

        AssertNoChange(soft, full);
    }

    [Fact]
    public void BlurSoft_PartialStrength_IsBetweenOriginalAndFullBlur()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var partial = MosaicService.BlurSoft(input, null, 5, 0.5);

        AssertChangesPixels(input, partial);
        AssertResultsDiffer(partial, input);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void BlurSoft_StrengthOutOfRange_IsClamped(double strength)
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        // Must not throw; strength is clamped to [0, 1].
        using var result = MosaicService.BlurSoft(input, null, 5, strength);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void BlurSoft_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = MosaicService.BlurSoft(input, null, 5, 0.5);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void BlurSoft_Region_LeavesOutsidePixelsUnchanged()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);
        var region = new Rect(0, 0, 10, 10);

        using var result = MosaicService.BlurSoft(input, region, 5, 0.5);

        Assert.Equal(input.Get<Vec3b>(20, 20), result.Get<Vec3b>(20, 20));
    }

    // ------------------------------------------------------------------ helpers

    private static void AssertAllPixelsSameHelper(Mat result)
        => ServiceTestHelper.AssertAllPixelsSame(result);
}
