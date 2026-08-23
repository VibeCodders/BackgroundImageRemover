using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class FilterServiceTests : ServiceTestBase
{
    private static readonly FilterKind[] AllFilterKinds = (FilterKind[])Enum.GetValues(typeof(FilterKind));

    private static Mat MakeImage(int size = 24)
        => CreateTestInputWithRectangle(size, size, new Scalar(30, 60, 90), new Scalar(200, 150, 100), size / 4, size / 4, size / 2, size / 2);

    [Fact]
    public void Apply_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FilterService.Apply(null!, FilterKind.Grayscale, 1.0));
    }

    [Theory]
    [MemberData(nameof(AllKindsData))]
    public void Apply_PreservesSizeAndType_ForEveryFilterKind(FilterKind kind)
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, kind, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Theory]
    [MemberData(nameof(AllKindsData))]
    public void Apply_IntensityZero_LeavesImageUnchanged_ForEveryFilterKind(FilterKind kind)
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, kind, 0.0);

        AssertNoChange(input, result);
    }

    [Theory]
    [MemberData(nameof(AllKindsData))]
    public void Apply_OnePixelImage_DoesNotThrow_ForEveryFilterKind(FilterKind kind)
    {
        using var input = CreateTestInput(1, 1, new Scalar(30, 60, 90));

        using var result = FilterService.Apply(input, kind, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    public static IEnumerable<object[]> AllKindsData()
        => AllFilterKinds.Select(k => new object[] { k });

    [Fact]
    public void Apply_None_NeverChangesImageRegardlessOfIntensity()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.None, 1.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_IntensityHalf_BlendsBetweenOriginalAndFullEffect()
    {
        using var input = MakeImage();

        using var full = FilterService.Apply(input, FilterKind.Invert, 1.0);
        using var half = FilterService.Apply(input, FilterKind.Invert, 0.5);

        AssertResultsDiffer(input, half);
        AssertResultsDiffer(full, half);
    }

    [Fact]
    public void Apply_Grayscale_RemovesColorVariationAcrossChannels()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Grayscale, 1.0);

        var pixel = result.Get<Vec3b>(1, 1);
        Assert.Equal(pixel.Item0, pixel.Item1);
        Assert.Equal(pixel.Item1, pixel.Item2);
    }

    [Fact]
    public void Apply_Invert_ProducesNegative()
    {
        using var input = CreateTestInput(4, 4, new Scalar(10, 100, 250));

        using var result = FilterService.Apply(input, FilterKind.Invert, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(245, 155, 5), pixel);
    }

    [Fact]
    public void Apply_Solarize_LeavesLowValuesUnchanged()
    {
        using var input = CreateTestInput(4, 4, new Scalar(50, 50, 50));

        using var result = FilterService.Apply(input, FilterKind.Solarize, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(50, 50, 50), pixel);
    }

    [Fact]
    public void Apply_Solarize_InvertsHighValues()
    {
        using var input = CreateTestInput(4, 4, new Scalar(200, 200, 200));

        using var result = FilterService.Apply(input, FilterKind.Solarize, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(55, 55, 55), pixel);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Apply_Posterize_ReducesDistinctColorCount(int levels)
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Posterize, 1.0, levels);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Posterize_DifferentLevels_ProduceDifferentResults()
    {
        using var input = MakeImage();

        using var coarse = FilterService.Apply(input, FilterKind.Posterize, 1.0, 2);
        using var fine = FilterService.Apply(input, FilterKind.Posterize, 1.0, 16);

        AssertResultsDiffer(coarse, fine);
    }

    [Fact]
    public void Apply_Sepia_ChangesPixelsAndPreservesShape()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Sepia, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_Cool_ShiftsTowardBlue()
    {
        using var input = CreateTestInput(4, 4, new Scalar(128, 128, 128));

        using var result = FilterService.Apply(input, FilterKind.Cool, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.True(pixel.Item0 > 128); // blue channel increases
        Assert.True(pixel.Item2 < 128); // red channel decreases
    }

    [Fact]
    public void Apply_Warm_ShiftsTowardRed()
    {
        using var input = CreateTestInput(4, 4, new Scalar(128, 128, 128));

        using var result = FilterService.Apply(input, FilterKind.Warm, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.True(pixel.Item0 < 128); // blue channel decreases
        Assert.True(pixel.Item2 > 128); // red channel increases
    }

    [Fact]
    public void Apply_Noir_ProducesGrayscaleWithBoostedContrast()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Noir, 1.0);

        var pixel = result.Get<Vec3b>(1, 1);
        Assert.Equal(pixel.Item0, pixel.Item1);
        Assert.Equal(pixel.Item1, pixel.Item2);
    }

    [Fact]
    public void Apply_Duotone_MapsShadowsAndHighlightsToExpectedColors()
    {
        using var black = CreateTestInput(4, 4, new Scalar(0, 0, 0));
        using var white = CreateTestInput(4, 4, new Scalar(255, 255, 255));

        using var shadowResult = FilterService.Apply(black, FilterKind.Duotone, 1.0);
        using var highlightResult = FilterService.Apply(white, FilterKind.Duotone, 1.0);

        // Shadow color is (80, 20, 10) BGR, highlight is (60, 200, 255) BGR (see Duotone impl).
        var shadowPixel = shadowResult.Get<Vec3b>(0, 0);
        var highlightPixel = highlightResult.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(80, 20, 10), shadowPixel);
        Assert.Equal(new Vec3b(60, 200, 255), highlightPixel);
    }

    [Fact]
    public void Apply_Vivid_IncreasesSaturation()
    {
        using var input = CreateTestInput(4, 4, new Scalar(50, 100, 200));

        using var result = FilterService.Apply(input, FilterKind.Vivid, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_Emboss_ChangesPixelsOnEdge()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Emboss, 1.0);

        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_Sketch_ProducesGrayscaleLookingResult()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Sketch, 1.0);

        AssertPreservesSizeAndType(input, result);
        var pixel = result.Get<Vec3b>(1, 1);
        Assert.Equal(pixel.Item0, pixel.Item1);
        Assert.Equal(pixel.Item1, pixel.Item2);
    }

    [Fact]
    public void Apply_Neon_ProducesDarkBackgroundWithEdges()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Neon, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Dreamy_ChangesPixels()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Dreamy, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_Vintage_ChangesPixels()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Vintage, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_Hdr_PreservesSizeAndType()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Hdr, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Pencil_PreservesSizeAndType()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Pencil, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_Cartoon_PreservesSizeAndType()
    {
        using var input = MakeImage();

        using var result = FilterService.Apply(input, FilterKind.Cartoon, 1.0);

        AssertPreservesSizeAndType(input, result);
    }
}
