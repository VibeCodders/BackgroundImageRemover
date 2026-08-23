using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class CartoonServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(10, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = CartoonService.Apply(input, 3, 8, 5);

        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_WithEdges_ChangesPixels()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(60, 100, 160));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(200, 200, 200), -1);

        using var result = CartoonService.Apply(input, 3, 8, 5);

        ServiceTestHelper.AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_EdgeThresholdZero_DisablesOutlinePass()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(60, 100, 160));
        Cv2.Rectangle(input, new Rect(4, 4, 8, 8), new Scalar(200, 200, 200), -1);

        using var withEdges = CartoonService.Apply(input, 3, 8, 5);
        using var noEdges = CartoonService.Apply(input, 3, 8, 0);

        ServiceTestHelper.AssertChangesPixels(withEdges, noEdges);
    }

    [Fact]
    public void Apply_MoreQuantizationLevels_ChangesResult()
    {
        using var input = new Mat(16, 16, MatType.CV_8UC3, new Scalar(60, 100, 160));

        using var coarse = CartoonService.Apply(input, 3, 2, 0);
        using var fine = CartoonService.Apply(input, 3, 32, 0);

        ServiceTestHelper.AssertChangesPixels(coarse, fine);
    }

    [Fact]
    public void Apply_UniformImage_QuantizesWithoutOutlines()
    {
        // A uniform image has no edges, so with edges enabled the result should still
        // be a single flat color (the quantized version of the input).
        using var input = new Mat(12, 12, MatType.CV_8UC3, new Scalar(127, 127, 127));
        using var result = CartoonService.Apply(input, 3, 8, 5);

        Vec3b first = result.Get<Vec3b>(0, 0);
        bool uniform = true;
        for (int y = 0; y < result.Height && uniform; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                if (result.Get<Vec3b>(y, x) != first)
                {
                    uniform = false;
                    break;
                }
            }
        }

        Assert.True(uniform);
    }
}
