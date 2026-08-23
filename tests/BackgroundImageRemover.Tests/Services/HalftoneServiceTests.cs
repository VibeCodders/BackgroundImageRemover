using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class HalftoneServiceTests : ServiceTestBase
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = CreateTestInput(16, 16, new Scalar(40, 90, 140));
        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_WithDarkArea_ChangesPixels()
    {
        using var input = CreateTestInput(16, 16, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_UniformWhite_ProducesBlankWhiteCanvas()
    {
        using var input = CreateTestInput(16, 16, new Scalar(255, 255, 255));
        using var result = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);

        // Nothing dark → no dots → the whole canvas stays white.
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                Assert.Equal(new Vec3b(255, 255, 255), result.Get<Vec3b>(y, x));
            }
        }
    }

    [Fact]
    public void Apply_Invert_ChangesResult()
    {
        using var input = CreateTestInput(16, 16, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var normal = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);
        using var inverted = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), true);

        AssertChangesPixels(normal, inverted);
    }

    [Fact]
    public void Apply_DifferentDotColor_ChangesResult()
    {
        using var input = CreateTestInput(16, 16, new Scalar(250, 250, 250));
        Cv2.Rectangle(input, new Rect(2, 2, 8, 8), new Scalar(10, 10, 10), -1);

        using var black = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 20), false);
        using var red = HalftoneService.Apply(input, 4, new Vec3b(20, 20, 220), false);

        AssertChangesPixels(black, red);
    }
}
