using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;

// Note: OpenCvSharp also defines a FlipMode enum, so the project-level enum is referenced as
// ImageFlipMode to avoid ambiguity.
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class FlipServiceTests
{
    // A 4×6 image with a single bright pixel in the top-left corner so each flip is verifiable.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public void Flip_Horizontal_MirrorsColumns()
    {
        using var input = CreateImageWithMarker();
        // Marker at top-left (0,0) -> after horizontal flip it is at top-right (Width-1,0).
        input.Set(0, 0, new Vec3b(0, 0, 255));

        using var result = FlipService.Flip(input, ImageFlipMode.Horizontal);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
        var px = result.Get<Vec3b>(0, Width - 1);
        Assert.Equal(255, px.Item2); // red moved to the right edge
    }

    [Fact]
    public void Flip_Vertical_MirrorsRows()
    {
        using var input = CreateImageWithMarker();
        input.Set(0, 0, new Vec3b(0, 0, 255)); // top-left

        using var result = FlipService.Flip(input, ImageFlipMode.Vertical);

        Assert.Equal(input.Size(), result.Size());
        var px = result.Get<Vec3b>(Height - 1, 0);
        Assert.Equal(255, px.Item2); // red moved to the bottom edge
    }

    [Fact]
    public void Flip_Both_180DegreeRotation()
    {
        using var input = CreateImageWithMarker();
        input.Set(0, 0, new Vec3b(0, 0, 255)); // top-left

        using var result = FlipService.Flip(input, ImageFlipMode.Both);

        Assert.Equal(input.Size(), result.Size());
        var px = result.Get<Vec3b>(Height - 1, Width - 1);
        Assert.Equal(255, px.Item2); // red moved to bottom-right
    }

    [Theory]
    [InlineData(ImageFlipMode.Horizontal)]
    [InlineData(ImageFlipMode.Vertical)]
    [InlineData(ImageFlipMode.Both)]
    public void Flip_PreservesSizeAndType(ImageFlipMode mode)
    {
        using var input = CreateImageWithMarker();

        using var result = FlipService.Flip(input, mode);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Flip_Horizontal_ThenHorizontal_RestoresOriginal()
    {
        using var input = CreateImageWithMarker();
        input.Set(1, 2, new Vec3b(0, 0, 255));

        using var once = FlipService.Flip(input, ImageFlipMode.Horizontal);
        using var twice = FlipService.Flip(once, ImageFlipMode.Horizontal);

        ServiceTestHelper.AssertNoChange(input, twice);
    }

    [Fact]
    public void Flip_NullInput_ReturnsEmptyMat()
    {
        using var result = FlipService.Flip(null!, ImageFlipMode.Horizontal);
        Assert.True(result.Empty());
    }

    [Fact]
    public void Flip_EmptyInput_ReturnsEmptyMat()
    {
        using var empty = new Mat();
        using var result = FlipService.Flip(empty, ImageFlipMode.Vertical);
        Assert.True(result.Empty());
    }

    private static Mat CreateImageWithMarker()
    {
        // A distinct gradient so pixels are identifiable by position.
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
