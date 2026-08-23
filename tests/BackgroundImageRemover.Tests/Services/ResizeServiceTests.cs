using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the resize helpers' aspect-ratio math and the empty-source hardening: an empty Mat
/// used to crash the ratio-based methods with a divide-by-zero, which could take down the
/// Resize/Export tools on degenerate images.
/// </summary>
public class ResizeServiceTests
{
    private const ResampleMethod Nearest = ResampleMethod.Nearest;

    private static Mat MakeImage(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ Basic resizes

    [Fact]
    public void ResizeTo_ExactSize()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizeTo(src, 50, 25, Nearest);

        Assert.Equal(new Size(50, 25), result.Size());
    }

    [Fact]
    public void ResizeToWidth_PreservesAspectRatio()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizeToWidth(src, 50, Nearest);

        Assert.Equal(new Size(50, 25), result.Size());
    }

    [Fact]
    public void ResizeToHeight_PreservesAspectRatio()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizeToHeight(src, 25, Nearest);

        Assert.Equal(new Size(50, 25), result.Size());
    }

    [Fact]
    public void ResizePercent_ScalesBothDimensions()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizePercent(src, 0.5, Nearest);

        Assert.Equal(new Size(100, 50), result.Size());
    }

    [Fact]
    public void ResizePercent_TinyPercent_KeepsAtLeastOnePixel()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizePercent(src, 0.001, Nearest);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    // ------------------------------------------------------------------ FitWithin / FillTo

    [Fact]
    public void FitWithin_LandscapeInSquareBox_FitsByWidth()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.FitWithin(src, 100, 100, Nearest);

        Assert.Equal(new Size(100, 50), result.Size());
    }

    [Fact]
    public void FitWithin_PortraitInSquareBox_FitsByHeight()
    {
        using var src = MakeImage(100, 200, new Scalar(10, 20, 30));

        using var result = ResizeService.FitWithin(src, 100, 100, Nearest);

        Assert.Equal(new Size(50, 100), result.Size());
    }

    [Fact]
    public void FillTo_CoversTheBoxAndCropsOverflow()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.FillTo(src, 100, 100, Nearest);

        Assert.Equal(new Size(100, 100), result.Size());
    }

    // ------------------------------------------------------------------ Longest side / megapixels

    [Fact]
    public void ResizeToLongestSide_ScalesLandscapeByWidth()
    {
        using var src = MakeImage(200, 100, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizeToLongestSide(src, 50, Nearest);

        Assert.Equal(new Size(50, 25), result.Size());
    }

    [Fact]
    public void ResizeToMegapixels_TargetsRequestedArea()
    {
        using var src = MakeImage(1000, 1000, new Scalar(10, 20, 30));

        using var result = ResizeService.ResizeToMegapixels(src, 0.25, Nearest);

        Assert.Equal(new Size(500, 500), result.Size());
    }

    // ------------------------------------------------------------------ Empty-source hardening

    [Fact]
    public void EmptySource_AllMethods_ReturnEmptyCloneWithoutThrowing()
    {
        using var empty = new Mat(0, 0, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using (var r = ResizeService.ResizeTo(empty, 100, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.ResizePercent(empty, 0.5, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.ResizeToWidth(empty, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.ResizeToHeight(empty, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.FitWithin(empty, 100, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.FillTo(empty, 100, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.ResizeToLongestSide(empty, 100, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
        using (var r = ResizeService.ResizeToMegapixels(empty, 1.0, Nearest)) Assert.Equal(new Size(0, 0), r.Size());
    }
}
