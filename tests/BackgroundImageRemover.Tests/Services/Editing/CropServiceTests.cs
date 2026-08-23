using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services.Editing;

/// <summary>
/// Pins the crop/trim helpers' contracts, including the degenerate-input hardening: an empty
/// or tiny source must never crash (Math.Clamp with min &gt; max used to throw on 0-sized images).
/// </summary>
public class CropServiceTests
{
    private static Mat MakeImage(int width, int height, Vec3b color)
        => new(height, width, MatType.CV_8UC3, new Scalar(color.Item0, color.Item1, color.Item2));

    // ------------------------------------------------------------------ CropRect

    [Fact]
    public void CropRect_FullyInside_ReturnsRequestedRect()
    {
        using var src = MakeImage(100, 50, new Vec3b(10, 20, 30));

        using var result = CropService.CropRect(src, new Rect(10, 5, 20, 10));

        Assert.Equal(new Size(20, 10), result.Size());
        Assert.Equal(new Vec3b(10, 20, 30), result.Get<Vec3b>(0, 0));
    }

    [Fact]
    public void CropRect_OutOfBounds_IsClampedToImage()
    {
        using var src = MakeImage(100, 50, new Vec3b(10, 20, 30));

        using var result = CropService.CropRect(src, new Rect(95, 45, 20, 20));

        Assert.Equal(new Size(5, 5), result.Size());
    }

    // ------------------------------------------------------------------ CenteredRectForSize

    [Fact]
    public void CenteredRectForSize_CentersWithinImage()
    {
        var rect = CropService.CenteredRectForSize(new Size(100, 50), 20, 10);

        Assert.Equal(new Rect(40, 20, 20, 10), rect);
    }

    [Fact]
    public void CenteredRectForSize_RequestedLargerThanImage_ClampsToImage()
    {
        var rect = CropService.CenteredRectForSize(new Size(100, 50), 500, 500);

        Assert.Equal(new Rect(0, 0, 100, 50), rect);
    }

    [Fact]
    public void CenteredRectForSize_EmptyImage_ReturnsEmptyRectWithoutThrowing()
    {
        var rect = CropService.CenteredRectForSize(new Size(0, 0), 20, 10);

        Assert.Equal(new Rect(0, 0, 0, 0), rect);
    }

    // ------------------------------------------------------------------ CenteredRectForAspect

    [Fact]
    public void CenteredRectForAspect_TallerRatio_UsesWidth()
    {
        // Image 200x100, requested ratio 2 (w/h): the width already matches, so the crop is
        // the full width and the centered height.
        var rect = CropService.CenteredRectForAspect(new Size(200, 100), 2.0);

        Assert.Equal(new Rect(0, 0, 200, 100), rect);
    }

    [Fact]
    public void CenteredRectForAspect_WiderRatio_CropsSides()
    {
        // Image 200x100, requested ratio 1: height matches, width becomes 100.
        var rect = CropService.CenteredRectForAspect(new Size(200, 100), 1.0);

        Assert.Equal(new Rect(50, 0, 100, 100), rect);
    }

    [Fact]
    public void CenteredRectForAspect_DegenerateInput_ReturnsFullImage()
    {
        Assert.Equal(new Rect(0, 0, 100, 50), CropService.CenteredRectForAspect(new Size(100, 50), 0));
        Assert.Equal(new Rect(0, 0, 0, 0), CropService.CenteredRectForAspect(new Size(0, 0), 1.0));
    }

    // ------------------------------------------------------------------ CropMargins

    [Fact]
    public void CropMargins_RemovesRequestedMargins()
    {
        using var src = MakeImage(100, 50, new Vec3b(10, 20, 30));

        using var result = CropService.CropMargins(src, 10, 5, 10, 5);

        Assert.Equal(new Size(80, 40), result.Size());
        Assert.Equal(new Vec3b(10, 20, 30), result.Get<Vec3b>(0, 0));
    }

    [Fact]
    public void CropMargins_HugeMargins_KeepAtLeastOnePixel()
    {
        using var src = MakeImage(100, 50, new Vec3b(10, 20, 30));

        using var result = CropService.CropMargins(src, 1000, 1000, 1000, 1000);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    [Fact]
    public void CropMargins_EmptySource_ReturnsEmptyCloneWithoutThrowing()
    {
        using var src = MakeImage(0, 0, new Vec3b(10, 20, 30));

        using var result = CropService.CropMargins(src, 10, 10, 10, 10);

        Assert.Equal(new Size(0, 0), result.Size());
    }

    // ------------------------------------------------------------------ TrimContentBounds

    [Fact]
    public void TrimContentBounds_UniformImage_ReturnsFullBounds()
    {
        using var src = MakeImage(100, 50, new Vec3b(10, 20, 30));

        var bounds = CropService.TrimContentBounds(src);

        Assert.Equal(new Rect(0, 0, 100, 50), bounds);
    }

    [Fact]
    public void TrimContentBounds_DetectsSubjectWithinFlatBorder()
    {
        using var src = MakeImage(100, 100, new Vec3b(10, 20, 30));
        using (var subject = new Mat(src, new Rect(30, 40, 40, 20)))
        {
            subject.SetTo(new Scalar(200, 200, 200));
        }

        var bounds = CropService.TrimContentBounds(src);

        Assert.Equal(new Rect(30, 40, 40, 20), bounds);
    }

    [Fact]
    public void TrimContentBounds_TinyImage_ReturnsFullBounds()
    {
        using var src = MakeImage(2, 2, new Vec3b(10, 20, 30));

        Assert.Equal(new Rect(0, 0, 2, 2), CropService.TrimContentBounds(src));
    }
}
