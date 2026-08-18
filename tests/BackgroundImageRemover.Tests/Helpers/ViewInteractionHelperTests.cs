using System.Windows;
using BackgroundImageRemover.Helpers;

namespace BackgroundImageRemover.Tests.Helpers;

public class ViewInteractionHelperTests
{
    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("PHOTO.PNG", true)]
    [InlineData("image.jpg", true)]
    [InlineData("image.jpeg", true)]
    [InlineData("image.bmp", true)]
    [InlineData("image.webp", true)]
    [InlineData("document.pdf", false)]
    [InlineData("script.cs", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedImage_ReturnsExpected(string? path, bool expected)
    {
        bool result = ViewInteractionHelper.IsSupportedImage(path!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeZoom_WhenZoomingIn_IncreasesScaleAndCentersCursor()
    {
        var cursor = new Point(100, 100);
        var currentTranslate = new Point(0, 0);
        double currentScale = 1.0;

        bool changed = ViewInteractionHelper.ComputeZoom(
            cursor,
            wheelDelta: 120,
            currentScale,
            currentTranslate,
            minScale: 1.0,
            maxScale: 8.0,
            out double newScale,
            out Point newTranslate);

        Assert.True(changed);
        Assert.True(newScale > currentScale);
        Assert.NotEqual(currentTranslate, newTranslate);
    }

    [Fact]
    public void ComputeZoom_ClampsToMaxScale()
    {
        var cursor = new Point(100, 100);
        var currentTranslate = new Point(0, 0);
        double currentScale = 8.0;

        bool changed = ViewInteractionHelper.ComputeZoom(
            cursor,
            wheelDelta: 120,
            currentScale,
            currentTranslate,
            minScale: 1.0,
            maxScale: 8.0,
            out double newScale,
            out _);

        Assert.False(changed);
        Assert.Equal(8.0, newScale);
    }

    [Fact]
    public void ComputePan_AddsDelta()
    {
        var startPoint = new Point(50, 50);
        var startTranslate = new Point(10, 20);
        var currentPoint = new Point(70, 40);

        var result = ViewInteractionHelper.ComputePan(startPoint, startTranslate, currentPoint);

        Assert.Equal(30, result.X);
        Assert.Equal(10, result.Y);
    }
}
