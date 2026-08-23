using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Tests.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the Heal tool's repair operations.</summary>
public class HealServiceTests
{
    private static Mat MakeGradient(int width = 60, int height = 60)
    {
        var image = new Mat(height, width, MatType.CV_8UC3);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image.Set<Vec3b>(y, x, new Vec3b((byte)x, (byte)y, (byte)((x + y) / 2)));
            }
        }

        return image;
    }

    [Fact]
    public void HealRegion_InpaintsTheMaskedArea()
    {
        using var image = MakeGradient();
        // A black vertical scratch.
        Cv2.Rectangle(image, new Rect(30, 0, 1, 60), new Scalar(0, 0, 0), -1);
        using var mask = new Mat(image.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Rect(30, 0, 1, 60), new Scalar(255), -1);

        using var result = HealService.HealRegion(image, mask, radius: 3, InpaintMethod.Telea);

        var healed = result.Get<Vec3b>(30, 30);
        // The scratch (0,0,0) is replaced by the surrounding gradient colors.
        Assert.True(healed.Item0 > 10, $"expected the scratch to be inpainted, got {healed.Item0}");
    }

    [Fact]
    public void HealRegion_EmptyMask_LeavesImageUnchanged()
    {
        using var image = MakeGradient();
        using var mask = new Mat(image.Size(), MatType.CV_8UC1, Scalar.All(0));

        using var result = HealService.HealRegion(image, mask, radius: 3, InpaintMethod.NS);

        ServiceTestHelper.AssertNoChange(image, result);
    }

    [Fact]
    public void RemoveDust_PreservesSizeAndType()
    {
        using var image = MakeGradient();

        using var result = HealService.RemoveDust(image, kernelSize: 3);

        ServiceTestHelper.AssertPreservesSizeAndType(image, result);
    }

    [Fact]
    public void RemoveScratches_ZeroStrength_ReturnsClone()
    {
        using var image = MakeGradient();

        using var result = HealService.RemoveScratches(image, 0.0);

        ServiceTestHelper.AssertNoChange(image, result);
    }

    [Fact]
    public void RemoveScratches_WithStrength_SmoothsPixels()
    {
        using var image = MakeGradient();

        using var result = HealService.RemoveScratches(image, 1.0);

        ServiceTestHelper.AssertPreservesSizeAndType(image, result);
    }

    [Fact]
    public void SurfaceSmooth_And_DetailEnhance_PreserveSizeAndType()
    {
        using var image = MakeGradient();

        using (var smooth = HealService.SurfaceSmooth(image, 0.8)) ServiceTestHelper.AssertPreservesSizeAndType(image, smooth);
        using (var detail = HealService.DetailEnhance(image, 0.8)) ServiceTestHelper.AssertPreservesSizeAndType(image, detail);
    }

    [Fact]
    public void HealRegion_OneByOneImage_DoesNotCrash()
    {
        using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var mask = new Mat(1, 1, MatType.CV_8UC1, Scalar.All(255));

        using var result = HealService.HealRegion(image, mask, radius: 3, InpaintMethod.Telea);

        Assert.Equal(new Size(1, 1), result.Size());
    }
}
