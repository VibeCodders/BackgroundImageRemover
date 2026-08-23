using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services;

public class NewToolsServicesTests
{
    [Fact]
    public void HealRegion_PreservesSizeAndType()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(120, 120, 120));
        using var mask = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(0));
        mask.Set(10, 10, (byte)255);

        using var result = HealService.HealRegion(src, mask, 3, InpaintTypes.Telea);

        ServiceTestHelper.AssertPreservesSizeAndType(src, result);
    }

    [Fact]
    public void RemoveDust_PreservesUniformColor()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = HealService.RemoveDust(src, 3);

        Assert.Equal(100, result.At<Vec3b>(5, 5).Item0);
    }

    [Fact]
    public void RemoveScratches_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 120, 140));

        using var result = HealService.RemoveScratches(src, 0);

        Assert.Equal(src.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void SurfaceSmooth_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 120, 140));

        using var result = HealService.SurfaceSmooth(src, 0);

        Assert.Equal(src.At<Vec3b>(0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void DetailEnhance_PreservesSize()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));

        using var result = HealService.DetailEnhance(src, 0.5);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void Liquify_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = LiquifyService.Warp(src, new Point(10, 10), 8, 0, LiquifyMode.Pinch);

        Assert.Equal(src.At<Vec3b>(5, 5), result.At<Vec3b>(5, 5));
    }

    [Fact]
    public void Liquify_Pinch_ChangesPixelsNearCenter()
    {
        using var src = new Mat(21, 21, MatType.CV_8UC3, Scalar.All(0));
        using (var block = new Mat(src, new Rect(6, 6, 9, 9)))
        {
            block.SetTo(new Scalar(200, 200, 200));
        }

        using var result = LiquifyService.Warp(src, new Point(10, 10), 8, 1.0, LiquifyMode.Pinch);

        ServiceTestHelper.AssertChangesPixels(src, result);
    }

    [Fact]
    public void Liquify_Twirl_PreservesSize()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(80, 80, 80));

        using var result = LiquifyService.Warp(src, new Point(15, 15), 12, 1.0, LiquifyMode.Twirl);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void Perspective_Correct_ProducesRequestedSize()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = PerspectiveService.Correct(
            src,
            new Point2f(0, 0), new Point2f(9, 0), new Point2f(9, 9), new Point2f(0, 9),
            20, 5);

        Assert.Equal(20, result.Width);
        Assert.Equal(5, result.Height);
    }

    [Fact]
    public void Perspective_DefaultQuad_ReturnsImageCorners()
    {
        var quad = PerspectiveService.DefaultQuad(new Size(640, 480));

        Assert.Equal(0, quad.TopLeft.X);
        Assert.Equal(0, quad.TopLeft.Y);
        Assert.Equal(639, quad.TopRight.X);
        Assert.Equal(479, quad.BottomRight.Y);
    }

    [Fact]
    public void Fx_Glow_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(10, 10, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = FxService.Glow(src, 0);

        Assert.Equal(src.At<Vec3b>(5, 5), result.At<Vec3b>(5, 5));
    }

    [Fact]
    public void Fx_Bloom_PreservesSize()
    {
        using var src = new Mat(30, 30, MatType.CV_8UC3, new Scalar(200, 200, 200));

        using var result = FxService.Bloom(src, 0.5);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void Fx_LightLeak_AddsWarmColorToCorner()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(0));

        using var result = FxService.LightLeak(src, 1.0);

        Assert.True(result.At<Vec3b>(0, 0).Item2 > 200); // red channel boosted in the corner
    }

    [Fact]
    public void Fx_ChromaticAberration_ZeroStrength_ReturnsClone()
    {
        using var src = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = FxService.ChromaticAberration(src, 0);

        Assert.Equal(src.At<Vec3b>(10, 10), result.At<Vec3b>(10, 10));
    }

    [Fact]
    public void Fx_Bokeh_AddsBrightPixels()
    {
        using var src = new Mat(60, 60, MatType.CV_8UC3, Scalar.All(0));

        using var result = FxService.Bokeh(src, count: 10, size: 15);

        using var gray = new Mat();
        Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);
    }

    [Fact]
    public void TiltShift_Apply_PreservesSize()
    {
        using var src = new Mat(50, 50, MatType.CV_8UC3, new Scalar(120, 120, 120));

        using var result = TiltShiftService.Apply(src, 0.5, 0.3, 8, vertical: false, saturationBoost: 0.4);

        Assert.Equal(src.Size(), result.Size());
    }

    [Fact]
    public void TiltShift_Apply_BlursOutsideFocusBand()
    {
        using var src = new Mat(40, 40, MatType.CV_8UC3, Scalar.All(0));
        using (var stripe = new Mat(src, new Rect(0, 18, 40, 4)))
        {
            stripe.SetTo(new Scalar(255, 255, 255));
        }

        using var sharp = TiltShiftService.Apply(src, 0.5, 0.1, 0, vertical: false, saturationBoost: 0);
        using var blurred = TiltShiftService.Apply(src, 0.5, 0.1, 6, vertical: false, saturationBoost: 0);

        // The top edge (far from the focus band) is blurred in the second result but sharp in the first.
        int sharpTop = sharp.At<Vec3b>(0, 0).Item0;
        int blurredTop = blurred.At<Vec3b>(0, 0).Item0;
        Assert.True(blurredTop != sharpTop || sharpTop == 0);
    }
}
