using BackgroundImageRemover.Helpers;
using System.Windows;

namespace BackgroundImageRemover.Tests.Helpers;

public class CoordinateMapperTests
{
    [Fact]
    public void ImageControlContentRect_CentersLetterboxedContent_WhenControlIsWiderThanImage()
    {
        // 400x200 control, 100x100 (square) image -> scaled to 200x200, centered horizontally.
        var content = CoordinateMapper.ImageControlContentRect(400, 200, 100, 100);

        Assert.Equal(200, content.Width, precision: 3);
        Assert.Equal(200, content.Height, precision: 3);
        Assert.Equal(100, content.X, precision: 3); // (400-200)/2
        Assert.Equal(0, content.Y, precision: 3);
    }

    [Fact]
    public void ControlPointToImagePixel_MapsCenterOfControl_ToCenterOfImage()
    {
        var pixel = CoordinateMapper.ControlPointToImagePixel(new Point(200, 100), 400, 200, 100, 100);

        Assert.Equal(50, pixel.X, precision: 3);
        Assert.Equal(50, pixel.Y, precision: 3);
    }

    [Fact]
    public void ControlPointToImagePixel_ClampsPointsOutsideTheLetterboxedContent()
    {
        // Click in the left letterbox padding (before the image starts).
        var pixel = CoordinateMapper.ControlPointToImagePixel(new Point(10, 100), 400, 200, 100, 100);

        Assert.Equal(0, pixel.X, precision: 3);
    }

    [Fact]
    public void ControlPointToImagePixel_ClampsToLastValidPixel_OnRightAndBottomEdges()
    {
        // A click on the very right edge of the letterboxed content must map to width - 1,
        // not width: tools round and index into the image, where x == width is out of bounds.
        var right = CoordinateMapper.ControlPointToImagePixel(new Point(400, 100), 400, 200, 100, 100);
        var bottom = CoordinateMapper.ControlPointToImagePixel(new Point(200, 200), 400, 200, 100, 100);
        var corner = CoordinateMapper.ControlPointToImagePixel(new Point(400, 200), 400, 200, 100, 100);

        Assert.Equal(99, right.X, precision: 3);
        Assert.Equal(50, right.Y, precision: 3);
        Assert.Equal(50, bottom.X, precision: 3);
        Assert.Equal(99, bottom.Y, precision: 3);
        Assert.Equal(99, corner.X, precision: 3);
        Assert.Equal(99, corner.Y, precision: 3);
    }

    [Fact]
    public void ControlPointToImagePixel_StillClampsPointsFarOutsideTheContent()
    {
        var pixel = CoordinateMapper.ControlPointToImagePixel(new Point(5000, 5000), 400, 200, 100, 100);

        Assert.Equal(99, pixel.X, precision: 3);
        Assert.Equal(99, pixel.Y, precision: 3);
    }

    [Fact]
    public void ToCvRect_RoundsAndEnforcesMinimumSize()
    {
        var rect = new Rect(1.2, 2.7, 0, 0).ToCvRect();

        Assert.True(rect.Width >= 1);
        Assert.True(rect.Height >= 1);
    }

    [Fact]
    public void ImagePixelToControlPoint_RoundTripsWithControlPointToImagePixel()
    {
        var control = CoordinateMapper.ImagePixelToControlPoint(new Point(25, 75), 400, 200, 100, 100);
        var pixel = CoordinateMapper.ControlPointToImagePixel(control, 400, 200, 100, 100);

        Assert.Equal(25, pixel.X, precision: 3);
        Assert.Equal(75, pixel.Y, precision: 3);
    }

    [Fact]
    public void ImageRectToControlRect_MatchesImageControlContentRect()
    {
        var ctl = CoordinateMapper.ImageRectToControlRect(new OpenCvSharp.Rect(0, 0, 100, 100), 400, 200, 100, 100);

        Assert.Equal(100, ctl.X, precision: 3);
        Assert.Equal(0, ctl.Y, precision: 3);
        Assert.Equal(200, ctl.Width, precision: 3);
        Assert.Equal(200, ctl.Height, precision: 3);
    }
}
