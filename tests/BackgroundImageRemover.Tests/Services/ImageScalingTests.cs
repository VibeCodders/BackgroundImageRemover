using BackgroundImageRemover.Services.Preview;

namespace BackgroundImageRemover.Tests.Services;

public class ImageScalingTests
{
    [Fact]
    public void ComputeFitSize_LandscapeImage_ScalesWidthToMaxDim()
    {
        var size = ImageScaling.ComputeFitSize(2000, 1000, 800);

        Assert.Equal(800, size.Width);
        Assert.Equal(400, size.Height);
    }

    [Fact]
    public void ComputeFitSize_PortraitImage_ScalesHeightToMaxDim()
    {
        var size = ImageScaling.ComputeFitSize(1000, 2000, 800);

        Assert.Equal(400, size.Width);
        Assert.Equal(800, size.Height);
    }

    [Fact]
    public void ComputeFitSize_SquareImage_ScalesBothSidesToMaxDim()
    {
        var size = ImageScaling.ComputeFitSize(1000, 1000, 800);

        Assert.Equal(800, size.Width);
        Assert.Equal(800, size.Height);
    }

    [Fact]
    public void ComputeFitSize_NeverProducesAZeroDimension_ForExtremeAspectRatios()
    {
        var size = ImageScaling.ComputeFitSize(1, 5000, 800);

        Assert.True(size.Width >= 1);
        Assert.True(size.Height >= 1);
    }

    [Fact]
    public void ComputeFitSize_UpscalesWhenSmallerThanMaxDim()
    {
        // The helper always fits to maxDim regardless of direction; callers decide whether to
        // skip calling it when no scaling is needed (as DownscaleService does).
        var size = ImageScaling.ComputeFitSize(100, 50, 800);

        Assert.Equal(800, size.Width);
        Assert.Equal(400, size.Height);
    }
}
