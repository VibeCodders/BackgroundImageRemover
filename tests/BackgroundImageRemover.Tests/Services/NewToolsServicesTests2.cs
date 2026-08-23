using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class NewToolsServicesTests2
{
    [Fact]
    public void BlurService_BlurAll_PreservesSizeAndType()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = BlurService.BlurAll(src, 5);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void BlurService_BlurRegion_OnlyAffectsMaskedArea()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var mask = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(0));
        // Leave one corner unmasked
        using var masked = new Mat(src, new Rect(0, 0, 5, 5));
        masked.SetTo(new Scalar(200, 200, 200));

        using var result = BlurService.BlurRegion(src, mask, 3);

        // The masked area (center) should be blurred from the original 100, the unmasked corner should be unchanged
        Assert.Equal(100, result.At<Vec3b>(15, 15).Item0);
        Assert.Equal(200, result.At<Vec3b>(2, 2).Item0);
    }

    [Fact]
    public void BlurService_MotionBlur_PreservesSize()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(120, 120, 120));

        using var result = BlurService.MotionBlur(src, 15, 45);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void SharpenService_SharpenAll_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 120, 140));

        using var result = SharpenService.SharpenAll(src, 0);

        Assert.Equal(src.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void SharpenService_SharpenRegion_PreservesSize()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var mask = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(255));

        using var result = SharpenService.SharpenRegion(src, mask, 0.5);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void VignetteService_Apply_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 120, 140));

        using var result = VignetteService.Apply(src, 0);

        Assert.Equal(src.At<Vec3b>(5, 5), result.At<Vec3b>(5, 5));
    }

    [Fact]
    public void VignetteService_Apply_DarkensCorners()
    {
        using var src = new Mat(40, 40, MatType.CV_8UC3, new Scalar(200, 200, 200));

        using var result = VignetteService.Apply(src, 0.5);

        // Corner should be darker than center
        int corner = result.At<Vec3b>(0, 0).Item0;
        int center = result.At<Vec3b>(20, 20).Item0;
        Assert.True(corner < center);
    }

    [Fact]
    public void VignetteService_Apply_Invert_LightensCorners()
    {
        using var src = new Mat(40, 40, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = VignetteService.Apply(src, 0.8, invert: true);

        // With invert, corners should be brighter than the original
        int corner = result.At<Vec3b>(0, 0).Item0;
        Assert.True(corner > 100);
    }

    [Fact]
    public void ColorPickerService_Sample_ReturnsCorrectPixel()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(50, 100, 150));

        var result = ColorPickerService.Sample(src, 5, 5);

        Assert.Equal(50, result.Item0);  // B
        Assert.Equal(100, result.Item1); // G
        Assert.Equal(150, result.Item2); // R
    }

    [Fact]
    public void ColorPickerService_Sample_ClampsOutOfBounds()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(50, 100, 150));

        var result = ColorPickerService.Sample(src, -5, 100);

        Assert.Equal(50, result.Item0);
    }

    [Fact]
    public void ColorPickerService_ToHex_ReturnsCorrectFormat()
    {
        var bgr = new Vec3b(50, 100, 150); // BGR

        var hex = ColorPickerService.ToHex(bgr);

        // BGR -> RGB: R=150, G=100, B=50 -> #966432
        Assert.Equal("#966432", hex);
    }

    [Fact]
    public void ColorPickerService_ToHsv_ReturnsValidRange()
    {
        using var src = new Mat(1, 1, MatType.CV_8UC3, new Scalar(50, 100, 150));

        var (h, s, v) = ColorPickerService.ToHsv(new Vec3b(50, 100, 150));

        Assert.InRange(h, 0, 360);
        Assert.InRange(s, 0, 100);
        Assert.InRange(v, 0, 100);
    }

    [Fact]
    public void EmojiOverlayService_Render_PreservesSize()
    {
        using var src = new Mat(50, 50, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = EmojiOverlayService.Render(src, EmojiOverlayService.EmojiKind.Star,
            new Point(25, 25), 20, new Vec3b(255, 255, 255), 1.0);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void EmojiOverlayService_RenderScatter_PreservesSize()
    {
        using var src = new Mat(60, 60, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = EmojiOverlayService.RenderScatter(src, EmojiOverlayService.EmojiKind.Heart,
            10, 10, 30, new Vec3b(255, 0, 0), 0.8);

        Assert.Equal(src.Size(), result.Size());
    }
}
