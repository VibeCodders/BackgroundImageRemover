using OpenCvSharp;
using BackgroundImageRemover.Services.Editing;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class PixelateServiceTests
{
    [Fact]
    public void Pixelate_BlockSizeOne_ReturnsUnchangedImage()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC3);
        for (int y = 0; y < input.Rows; y++)
        {
            for (int x = 0; x < input.Cols; x++)
            {
                input.Set<Vec3b>(y, x, new Vec3b((byte)x, (byte)y, (byte)(x + y)));
            }
        }

        using var result = PixelateService.Pixelate(input, 1);

        Assert.Equal(0, Cv2.Norm(input, result, NormTypes.L1));
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Pixelate_BlockSizeTen_PixelatesRegion()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC3);
        for (int y = 0; y < input.Rows; y++)
        {
            for (int x = 0; x < input.Cols; x++)
            {
                input.Set<Vec3b>(y, x, new Vec3b((byte)x, (byte)y, (byte)(x + y)));
            }
        }

        using var result = PixelateService.Pixelate(input, 10);
        var expected = input.Get<Vec3b>(0, 0);

        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Assert.Equal(expected, result.Get<Vec3b>(y, x));
            }
        }

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
