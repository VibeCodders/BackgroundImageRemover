using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class TransformServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    private static Mat MakeMarker(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.All(0));
        mat.Set(0, 0, new Vec3b(0, 0, 255)); // red marker at top-left
        return mat;
    }

    // ------------------------------------------------------------------ FlipHorizontal / FlipVertical

    [Fact]
    public void FlipHorizontal_MovesMarkerToOppositeColumn()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.FlipHorizontal(input);

        AssertPreservesSizeAndType(input, result);
        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(0, 5));
        Assert.Equal(new Vec3b(0, 0, 0), result.Get<Vec3b>(0, 0));
    }

    [Fact]
    public void FlipVertical_MovesMarkerToOppositeRow()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.FlipVertical(input);

        AssertPreservesSizeAndType(input, result);
        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(3, 0));
    }

    // ------------------------------------------------------------------ Rotate90 CW / CCW / 180

    [Fact]
    public void Rotate90Clockwise_SwapsDimensions()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.Rotate90Clockwise(input);

        Assert.Equal(4, result.Width);
        Assert.Equal(6, result.Height);
        // Top-left marker moves to top-right after a clockwise rotation.
        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(0, 3));
    }

    [Fact]
    public void Rotate90CounterClockwise_SwapsDimensions()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.Rotate90CounterClockwise(input);

        Assert.Equal(4, result.Width);
        Assert.Equal(6, result.Height);
        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(5, 0));
    }

    [Fact]
    public void Rotate180_KeepsDimensions_MovesMarkerToOppositeCorner()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.Rotate180(input);

        AssertPreservesSizeAndType(input, result);
        Assert.Equal(new Vec3b(0, 0, 255), result.Get<Vec3b>(3, 5));
    }

    // ------------------------------------------------------------------ Rotate (arbitrary angle)

    [Fact]
    public void Rotate_ZeroDegrees_ReturnsClone()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.Rotate(input, 0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void Rotate_90Degrees_TransposesCanvas()
    {
        using var input = MakeMarker(6, 4);

        using var result = TransformService.Rotate(input, 90);

        Assert.Equal(4, result.Width);
        Assert.Equal(6, result.Height);
    }

    [Fact]
    public void Rotate_45Degrees_GrowsCanvas()
    {
        using var input = MakeMarker(10, 10);

        using var result = TransformService.Rotate(input, 45);

        Assert.True(result.Width > input.Width);
        Assert.True(result.Height > input.Height);
    }

    [Fact]
    public void Rotate_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = TransformService.Rotate(input, 33);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    // ------------------------------------------------------------------ Resize

    [Fact]
    public void Resize_ScalesDimensionsBySameFactor()
    {
        using var input = MakeUniform(10, 20, new Scalar(50, 100, 150));

        using var result = TransformService.Resize(input, 2.0);

        Assert.Equal(20, result.Width);
        Assert.Equal(40, result.Height);
    }

    [Fact]
    public void Resize_DownscalesCorrectly()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.Resize(input, 0.5);

        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void Resize_NonPositiveScale_ClampedToKeepAtLeastOnePixel()
    {
        using var input = MakeUniform(20, 10, new Scalar(50, 100, 150));

        using var result = TransformService.Resize(input, 0.0);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    [Fact]
    public void Resize_NegativeScale_ClampedToKeepAtLeastOnePixel()
    {
        using var input = MakeUniform(20, 10, new Scalar(50, 100, 150));

        using var result = TransformService.Resize(input, -5.0);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    [Fact]
    public void Resize_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = TransformService.Resize(input, 3.0);

        Assert.Equal(3, result.Width);
        Assert.Equal(3, result.Height);
    }

    // ------------------------------------------------------------------ ResizeTo

    [Fact]
    public void ResizeTo_SetsExactDimensions()
    {
        using var input = MakeUniform(10, 20, new Scalar(50, 100, 150));

        using var result = TransformService.ResizeTo(input, 33, 44);

        Assert.Equal(new Size(33, 44), result.Size());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    [InlineData(10, -5)]
    public void ResizeTo_NonPositiveDimensions_ClampedToOne(int width, int height)
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.ResizeTo(input, width, height);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    // ------------------------------------------------------------------ Skew

    [Fact]
    public void Skew_ZeroSkew_ReturnsClone()
    {
        using var input = MakeMarker(10, 10);

        using var result = TransformService.Skew(input, 0, 0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void Skew_HorizontalSkew_GrowsWidth()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.Skew(input, 20, 0);

        Assert.True(result.Width > input.Width);
        Assert.Equal(input.Height, result.Height);
    }

    [Fact]
    public void Skew_VerticalSkew_GrowsHeight()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.Skew(input, 0, 20);

        Assert.True(result.Height > input.Height);
        Assert.Equal(input.Width, result.Width);
    }

    [Fact]
    public void Skew_NegativeAngles_DoesNotThrow()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.Skew(input, -20, -20);

        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Theory]
    [InlineData(89.9)]
    [InlineData(-89.9)]
    public void Skew_NearAsymptoteAngle_DoesNotThrow(double angle)
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.Skew(input, angle, 0);

        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    // ------------------------------------------------------------------ CropToAspect

    [Fact]
    public void CropToAspect_WiderRatio_CropsSides()
    {
        using var input = MakeUniform(200, 100, new Scalar(50, 100, 150));

        using var result = TransformService.CropToAspect(input, 1.0);

        Assert.Equal(new Size(100, 100), result.Size());
    }

    // ------------------------------------------------------------------ TrimBorder

    [Fact]
    public void TrimBorder_UniformImage_ReturnsFullSize()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.TrimBorder(input);

        Assert.Equal(input.Size(), result.Size());
    }

    [Fact]
    public void TrimBorder_FlatBorderWithSubject_CropsToSubject()
    {
        using var input = MakeUniform(40, 40, new Scalar(10, 10, 10));
        using (var subject = new Mat(input, new Rect(10, 10, 15, 15)))
        {
            subject.SetTo(new Scalar(240, 240, 240));
        }

        using var result = TransformService.TrimBorder(input);

        Assert.Equal(new Size(15, 15), result.Size());
    }

    [Fact]
    public void TrimBorder_BgraImage_PreservesAlphaChannel()
    {
        using var bgr = MakeUniform(20, 20, new Scalar(10, 10, 10));
        using (var subject = new Mat(bgr, new Rect(5, 5, 8, 8)))
        {
            subject.SetTo(new Scalar(240, 240, 240));
        }
        using var alpha = new Mat(bgr.Size(), MatType.CV_8UC1, new Scalar(200));
        using var bgra = bgr.ToBgra(alpha);

        using var result = TransformService.TrimBorder(bgra);

        Assert.Equal(4, result.Channels());
        Assert.Equal(new Size(8, 8), result.Size());
    }

    // ------------------------------------------------------------------ Pad

    [Fact]
    public void Pad_ExpandsCanvasBySpecifiedMargins()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.Pad(input, 2, 3, 4, 5, Scalar.All(0));

        Assert.Equal(new Size(16, 18), result.Size());
        Assert.Equal(input.Get<Vec3b>(0, 0), result.Get<Vec3b>(3, 2));
    }

    // ------------------------------------------------------------------ ResizeToFit

    [Fact]
    public void ResizeToFit_LargerThanBox_ScalesDownPreservingAspect()
    {
        using var input = MakeUniform(200, 100, new Scalar(50, 100, 150));

        using var result = TransformService.ResizeToFit(input, 50, 50);

        Assert.Equal(50, result.Width);
        Assert.Equal(25, result.Height);
    }

    [Fact]
    public void ResizeToFit_SmallerThanBox_NeverUpscales()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.ResizeToFit(input, 100, 100);

        Assert.Equal(input.Size(), result.Size());
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    public void ResizeToFit_NonPositiveBoxDimensions_ClampedToOne(int maxW, int maxH)
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = TransformService.ResizeToFit(input, maxW, maxH);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    // ------------------------------------------------------------------ CropCenter

    [Fact]
    public void CropCenter_SmallerThanImage_CropsCenteredRegion()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));
        input.Set(10, 10, new Vec3b(1, 2, 3));

        using var result = TransformService.CropCenter(input, 10, 10, Scalar.All(0));

        Assert.Equal(new Size(10, 10), result.Size());
        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(5, 5));
    }

    [Fact]
    public void CropCenter_LargerThanImage_PadsWithFillColor()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.CropCenter(input, 20, 20, new Scalar(0, 0, 0));

        Assert.Equal(new Size(20, 20), result.Size());
        // Corner should be the fill color (outside the pasted original image).
        Assert.Equal(new Vec3b(0, 0, 0), result.Get<Vec3b>(0, 0));
        // Center should contain the original image content.
        Assert.Equal(new Vec3b(50, 100, 150), result.Get<Vec3b>(10, 10));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void CropCenter_NonPositiveDimensions_ClampedToOne(int width, int height)
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.CropCenter(input, width, height, Scalar.All(0));

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    // ------------------------------------------------------------------ Tile

    [Fact]
    public void Tile_RepeatsImageToFillLargerCanvas()
    {
        using var input = MakeUniform(5, 5, new Scalar(50, 100, 150));
        input.Set(0, 0, new Vec3b(1, 2, 3));

        using var result = TransformService.Tile(input, 12, 7);

        Assert.Equal(new Size(12, 7), result.Size());
        // The marker repeats at each tile origin.
        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(0, 0));
        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(0, 5));
        Assert.Equal(new Vec3b(1, 2, 3), result.Get<Vec3b>(5, 0));
    }

    [Fact]
    public void Tile_SmallerThanImage_CropsToRequestedCanvas()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = TransformService.Tile(input, 4, 4);

        Assert.Equal(new Size(4, 4), result.Size());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void Tile_NonPositiveCanvasDimensions_ClampedToOne(int width, int height)
    {
        using var input = MakeUniform(5, 5, new Scalar(50, 100, 150));

        using var result = TransformService.Tile(input, width, height);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    [Fact]
    public void Tile_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(50, 100, 150));

        using var result = TransformService.Tile(input, 5, 5);

        Assert.Equal(new Size(5, 5), result.Size());
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                Assert.Equal(new Vec3b(50, 100, 150), result.Get<Vec3b>(y, x));
            }
        }
    }

    // ------------------------------------------------------------------ EstimateSkewAngle / AutoStraighten

    [Fact]
    public void EstimateSkewAngle_UniformImage_ReturnsZero()
    {
        using var input = MakeUniform(50, 50, new Scalar(50, 100, 150));

        double angle = TransformService.EstimateSkewAngle(input);

        Assert.Equal(0.0, angle);
    }

    [Fact]
    public void EstimateSkewAngle_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(50, 100, 150));

        double angle = TransformService.EstimateSkewAngle(input);

        Assert.Equal(0.0, angle);
    }

    [Fact]
    public void AutoStraighten_UniformImage_ReturnsUnchangedClone()
    {
        using var input = MakeUniform(50, 50, new Scalar(50, 100, 150));

        using var result = TransformService.AutoStraighten(input);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void AutoStraighten_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(50, 100, 150));

        using var result = TransformService.AutoStraighten(input);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }
}
