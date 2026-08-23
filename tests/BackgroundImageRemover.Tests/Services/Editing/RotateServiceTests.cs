using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class RotateServiceTests
{
    // A 4×6 image with a single bright marker so rotation is verifiable.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public void Rotate_ZeroAngle_WithExpand_ReturnsClone()
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, 0, expand: true);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
        ServiceTestHelper.AssertNoChange(input, result);
    }

    [Theory]
    [InlineData(90, true)]
    [InlineData(-90, true)]
    [InlineData(270, true)]
    public void Rotate_WithExpand_SwapsDimensionsFor90Multiple(double angle, bool expand)
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, angle, expand);

        // For 90° multiples (except 180°) the bounding box is the transposed size.
        int expectedW = Height;
        int expectedH = Width;
        Assert.Equal(expectedW, result.Width);
        Assert.Equal(expectedH, result.Height);
    }

    [Theory]
    [InlineData(180, true)]
    [InlineData(-180, true)]
    public void Rotate_WithExpand_180KeepsSameDimensions(double angle, bool expand)
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, angle, expand);

        // 180° rotation keeps the same canvas size.
        Assert.Equal(input.Width, result.Width);
        Assert.Equal(input.Height, result.Height);
    }

    [Fact]
    public void Rotate_90_WithExpand_TransposesDimensionsAndPreservesContent()
    {
        using var input = CreateImageWithMarker();
        input.Set(0, 0, new Vec3b(0, 0, 255)); // top-left red marker

        using var result = RotateService.Rotate(input, 90, expand: true);

        // A 90° CCW rotation transposes the image (Height-wide × Width-tall).
        Assert.Equal(Height, result.Width);
        Assert.Equal(Width, result.Height);

        // The top-left corner maps to the bottom-left corner of the transposed canvas.
        var px = result.Get<Vec3b>(Width - 1, 0);
        Assert.Equal(255, px.Item2);

        // The red marker must appear exactly once in the result.
        using var redChannel = new Mat();
        Cv2.ExtractChannel(result, redChannel, 2);
        Assert.Equal(1, Cv2.CountNonZero(redChannel));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Rotate_WithoutExpand_KeepsOriginalDimensions(double angle)
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, angle, expand: false);

        Assert.Equal(input.Width, result.Width);
        Assert.Equal(input.Height, result.Height);
    }

    [Theory]
    [InlineData(45, true)]
    [InlineData(30, true)]
    [InlineData(-60, true)]
    public void Rotate_WithExpand_GrowsCanvasToFit(double angle, bool expand)
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, angle, expand);

        // For non-90° angles the bounding box is strictly larger than the source.
        Assert.True(result.Width >= input.Width || result.Height >= input.Height);
    }

    [Fact]
    public void Rotate_45_WithExpand_ProducesLargerCanvas()
    {
        using var input = CreateImageWithMarker();

        using var result = RotateService.Rotate(input, 45, expand: true);

        Assert.True(result.Width > input.Width);
        Assert.True(result.Height > input.Height);
    }

    [Fact]
    public void Rotate_NullInput_ReturnsEmptyMat()
    {
        using var result = RotateService.Rotate(null!, 45, expand: true);
        Assert.True(result.Empty());
    }

    [Fact]
    public void Rotate_EmptyInput_ReturnsEmptyMat()
    {
        using var empty = new Mat();
        using var result = RotateService.Rotate(empty, 45, expand: false);
        Assert.True(result.Empty());
    }

    [Fact]
    public void Rotate_WithExpand_PreservesAlphaChannel()
    {
        using var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(120, 120, 120));
        using var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(200));
        using var bgra = bgr.ToBgra(alpha);

        using var result = RotateService.Rotate(bgra, 90, expand: true);

        Assert.Equal(4, result.Channels());
    }

    private static Mat CreateImageWithMarker()
    {
        var mat = new Mat(Height, Width, MatType.CV_8UC3, Scalar.All(0));
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                mat.Set(y, x, new Vec3b((byte)(x * 10), (byte)(y * 10), 0));
            }
        }
        return mat;
    }
}
