using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class DuotoneServiceTests
{
    [Fact]
    public void Apply_ZeroStrength_ReturnsUnchangedImage()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = DuotoneService.Apply(input,
            new Vec3b(10, 10, 80), new Vec3b(255, 200, 40), 0.5, 0);

        ServiceTestHelper.AssertNoChange(input, result);
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_FullStrength_ChangesPixels()
    {
        using var input = new Mat(4, 4, MatType.CV_8UC3, new Scalar(60, 120, 180));
        using var result = DuotoneService.Apply(input,
            new Vec3b(10, 10, 80), new Vec3b(255, 200, 40), 0.5, 1);

        ServiceTestHelper.AssertChangesPixels(input, result);
    }

    [Fact]
    public void Apply_DarkPixelTendsTowardDarkColor()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3, new Scalar(5, 5, 5));
        using var result = DuotoneService.Apply(input,
            new Vec3b(0, 0, 60), new Vec3b(255, 220, 40), 0.5, 1);

        var pixel = result.Get<Vec3b>(0, 0);
        // Dark pixel maps near the dark color (blue channel, BGR value 60).
        Assert.True(pixel[0] <= 40);
    }
}
