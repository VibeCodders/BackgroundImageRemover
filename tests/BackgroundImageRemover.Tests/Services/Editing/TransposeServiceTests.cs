using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class TransposeServiceTests
{
    // A non-square image so the width/height swap is observable.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public void Transpose_SwapsWidthAndHeight()
    {
        using var input = CreateImageWithMarker();

        using var result = TransposeService.Transpose(input);

        Assert.Equal(Height, result.Width);
        Assert.Equal(Width, result.Height);
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Transpose_MirrorsAcrossMainDiagonal()
    {
        using var input = CreateImageWithMarker();
        input.Set(0, 0, new Vec3b(0, 0, 255)); // top-left red marker
        input.Set(1, 2, new Vec3b(0, 255, 0)); // green at (row=1, col=2)

        using var result = TransposeService.Transpose(input);

        // Transpose swaps (row, col) -> (col, row).
        var topLeft = result.Get<Vec3b>(0, 0);
        Assert.Equal(255, topLeft.Item2); // red still at top-left

        var green = result.Get<Vec3b>(2, 1);
        Assert.Equal(255, green.Item1); // green moved from (1,2) to (2,1)
    }

    [Fact]
    public void Transpose_Twice_RestoresOriginal()
    {
        using var input = CreateImageWithMarker();
        input.Set(1, 2, new Vec3b(0, 0, 255));

        using var once = TransposeService.Transpose(input);
        using var twice = TransposeService.Transpose(once);

        Assert.Equal(input.Size(), twice.Size());
        ServiceTestHelper.AssertNoChange(input, twice);
    }

    [Fact]
    public void Transpose_PreservesPixelData()
    {
        using var input = CreateImageWithMarker();
        input.Set(2, 3, new Vec3b(255, 128, 64));

        using var result = TransposeService.Transpose(input);

        var px = result.Get<Vec3b>(3, 2);
        Assert.Equal(255, px.Item0);
        Assert.Equal(128, px.Item1);
        Assert.Equal(64, px.Item2);
    }

    [Fact]
    public void Transpose_NullInput_ReturnsEmptyMat()
    {
        using var result = TransposeService.Transpose(null!);
        Assert.True(result.Empty());
    }

    [Fact]
    public void Transpose_EmptyInput_ReturnsEmptyMat()
    {
        using var empty = new Mat();
        using var result = TransposeService.Transpose(empty);
        Assert.True(result.Empty());
    }

    [Fact]
    public void Transpose_PreservesAlphaChannel()
    {
        using var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(120, 120, 120));
        using var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(200));
        using var bgra = bgr.ToBgra(alpha);

        using var result = TransposeService.Transpose(bgra);

        Assert.Equal(4, result.Channels());
        Assert.Equal(Height, result.Width);
        Assert.Equal(Width, result.Height);
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
