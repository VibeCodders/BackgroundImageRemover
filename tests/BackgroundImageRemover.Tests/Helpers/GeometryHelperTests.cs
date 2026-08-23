using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Pins the rectangle-clamping contract of <see cref="GeometryHelper.ClampToSize"/>: any
/// rectangle is squeezed into a positive-area rect inside the image, and a degenerate
/// (empty) image yields an empty rect instead of throwing inside Math.Clamp — which used
/// to crash the crop/region tools on 0-sized sources.
/// </summary>
public class GeometryHelperTests
{
    private static readonly Size Size100x50 = new(100, 50);

    [Fact]
    public void ClampToSize_FullyInsideRect_IsUnchanged()
    {
        var rect = new Rect(10, 5, 20, 10);

        var result = GeometryHelper.ClampToSize(Size100x50, rect);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void ClampToSize_PartiallyOutside_PushesInsideBounds()
    {
        var rect = new Rect(95, 45, 20, 20);

        var result = GeometryHelper.ClampToSize(Size100x50, rect);

        Assert.Equal(new Rect(95, 45, 5, 5), result);
    }

    [Fact]
    public void ClampToSize_NegativeOrigin_IsPushedToZero()
    {
        var rect = new Rect(-10, -5, 20, 10);

        var result = GeometryHelper.ClampToSize(Size100x50, rect);

        Assert.Equal(new Rect(0, 0, 20, 10), result);
    }

    [Fact]
    public void ClampToSize_FarOutside_YieldsOnePixelRectAtEdge()
    {
        var rect = new Rect(500, 300, 10, 10);

        var result = GeometryHelper.ClampToSize(Size100x50, rect);

        Assert.Equal(new Rect(99, 49, 1, 1), result);
    }

    [Fact]
    public void ClampToSize_ZeroAreaRect_GetsPositiveArea()
    {
        // A zero-width rect must not survive: OpenCV sub-Mats require width/height >= 1.
        var result = GeometryHelper.ClampToSize(Size100x50, new Rect(10, 10, 0, 0));

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
        Assert.Equal(10, result.X);
        Assert.Equal(10, result.Y);
    }

    [Fact]
    public void ClampToSize_EmptyImage_ReturnsEmptyRectWithoutThrowing()
    {
        var result = GeometryHelper.ClampToSize(new Size(0, 0), new Rect(5, 5, 10, 10));

        Assert.Equal(new Rect(0, 0, 0, 0), result);
    }

    [Fact]
    public void ClampToSize_EmptyHeight_ReturnsEmptyRectWithoutThrowing()
    {
        var result = GeometryHelper.ClampToSize(new Size(100, 0), new Rect(5, 5, 10, 10));

        Assert.Equal(new Rect(0, 0, 0, 0), result);
    }
}
