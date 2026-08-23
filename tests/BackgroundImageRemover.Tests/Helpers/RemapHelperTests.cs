using BackgroundImageRemover.Helpers;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Pins <see cref="RemapHelper.Remap"/> — the shared mapX/mapY parallel fill + <see cref="Cv2.Remap"/>
/// skeleton used by Wave, Liquify and FX. Verifies that the parallel fill produces the same maps as
/// a sequential reference, that identity/translation remaps behave like a direct Cv2.Remap call,
/// that constant and replicate borders are honored, and that ROI (non-continuous) sources work.
/// </summary>
public class RemapHelperTests
{
    [Fact]
    public void IdentityRemap_ReturnsIdenticalImage()
    {
        using var src = CreateGradient(17, 11);
        using var result = RemapHelper.Remap(src, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = x;
            mapYRow[x] = y;
        });

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
        ServiceTestHelper.AssertNoChange(src, result);
    }

    [Fact]
    public void ParallelFill_MatchesSequentialReference()
    {
        using var src = CreateGradient(16, 9);

        // Reference: build the same maps sequentially, then remap directly.
        int w = src.Width;
        int h = src.Height;
        using var refMapX = new Mat(h, w, MatType.CV_32FC1);
        using var refMapY = new Mat(h, w, MatType.CV_32FC1);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                refMapX.Set(y, x, (float)(x - 3 + 0.5 * Math.Sin(y / 3.0)));
                refMapY.Set(y, x, (float)(y - 2 + 0.25 * Math.Cos(x / 4.0)));
            }
        }

        using var expected = new Mat();
        Cv2.Remap(src, expected, refMapX, refMapY, InterpolationFlags.Linear, BorderTypes.Replicate);

        using var actual = RemapHelper.Remap(src, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = (float)(x - 3 + 0.5 * Math.Sin(y / 3.0));
            mapYRow[x] = (float)(y - 2 + 0.25 * Math.Cos(x / 4.0));
        });

        // Identical maps must produce pixel-identical output.
        ServiceTestHelper.AssertNoChange(expected, actual);
    }

    [Fact]
    public void Translation_WithReplicateBorder_ClampsToEdges()
    {
        using var src = CreateGradient(15, 10);
        const int dx = 3;
        const int dy = 2;

        using var result = RemapHelper.Remap(src, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = x - dx;
            mapYRow[x] = y - dy;
        });

        // Interior pixels sample from the shifted source.
        Assert.Equal(src.Get<Vec3b>(5, 7), result.Get<Vec3b>(5 + dy, 7 + dx));

        // Out-of-range samples clamp to the edge (replicate).
        Assert.Equal(src.Get<Vec3b>(0, 0), result.Get<Vec3b>(0, 0));
        // x < dx clamps to the left edge, y < dy clamps to the top edge.
        Assert.Equal(src.Get<Vec3b>(0, 0), result.Get<Vec3b>(0, dx - 1));
        Assert.Equal(src.Get<Vec3b>(0, 11), result.Get<Vec3b>(dy - 1, 14));
    }

    [Fact]
    public void ConstantBorder_AllSamplesOutOfBounds_ReturnsBorderValue()
    {
        using var src = CreateGradient(12, 8);
        int w = src.Width;
        int h = src.Height;

        // Every sample coordinate lies beyond the image, so the whole result is the border value.
        using var result = RemapHelper.Remap(src, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = x + w;
            mapYRow[x] = y + h;
        }, BorderTypes.Constant, Scalar.All(128));

        using var expected = new Mat(h, w, MatType.CV_8UC3, Scalar.All(128));
        ServiceTestHelper.AssertNoChange(expected, result);
    }

    [Fact]
    public void ReplicateBorder_ClampsSamplesToImageEdge()
    {
        using var src = CreateGradient(13, 7);
        int w = src.Width;

        // Map every column beyond the right edge: replicate clamps to the last column.
        using var result = RemapHelper.Remap(src, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = x + w;
            mapYRow[x] = y;
        });

        for (int y = 0; y < src.Height; y++)
        {
            Assert.Equal(src.Get<Vec3b>(y, w - 1), result.Get<Vec3b>(y, 0));
            Assert.Equal(src.Get<Vec3b>(y, w - 1), result.Get<Vec3b>(y, w - 1));
        }
    }

    [Fact]
    public void RoiView_IdentityRemap_MatchesRoiContent()
    {
        // A non-continuous view of a larger image: the maps honor the parent stride.
        using var parent = CreateGradient(20, 14);
        var roi = new Rect(3, 4, 7, 5);
        using var roiView = new Mat(parent, roi);

        using var result = RemapHelper.Remap(roiView, (x, y, mapXRow, mapYRow) =>
        {
            mapXRow[x] = x;
            mapYRow[x] = y;
        });

        Assert.Equal(roi.Size, result.Size());
        // Identity remap of the view reproduces exactly the region's original pixels.
        for (int y = 0; y < roi.Height; y++)
        {
            for (int x = 0; x < roi.Width; x++)
            {
                Assert.Equal(parent.Get<Vec3b>(roi.Y + y, roi.X + x), result.Get<Vec3b>(y, x));
            }
        }
    }

    [Fact]
    public void NullArguments_Throw()
    {
        using var src = new Mat(4, 4, MatType.CV_8UC3, Scalar.All(0));
        Assert.Throws<ArgumentNullException>(() => RemapHelper.Remap(null!, (_, _, _, _) => { }));
        Assert.Throws<ArgumentNullException>(() => RemapHelper.Remap(src, null!));
    }

    /// <summary>Builds a small BGR gradient where every pixel is a distinct color.</summary>
    private static Mat CreateGradient(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mat.Set(y, x, new Vec3b((byte)(x * 17), (byte)(y * 13), (byte)(x + y)));
            }
        }

        return mat;
    }
}
