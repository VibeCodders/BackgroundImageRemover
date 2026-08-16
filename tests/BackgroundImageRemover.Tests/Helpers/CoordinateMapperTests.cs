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
    public void ToCvRect_RoundsAndEnforcesMinimumSize()
    {
        var rect = new Rect(1.2, 2.7, 0, 0).ToCvRect();

        Assert.True(rect.Width >= 1);
        Assert.True(rect.Height >= 1);
    }
}
