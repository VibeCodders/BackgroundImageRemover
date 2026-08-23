using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class FxServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ Glow

    [Fact]
    public void Glow_ZeroStrength_ReturnsUnchangedClone()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.Glow(input, 0.0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void Glow_PositiveStrength_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.Glow(input, 0.7);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Glow_StrengthOutOfRange_IsClamped(double strength)
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.Glow(input, strength);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Glow_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = FxService.Glow(input, 0.5);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Glow_NullInput_ReturnsEmptyMat()
    {
        using var result = FxService.Glow(null!, 0.5);

        Assert.True(result.Empty());
    }

    // ------------------------------------------------------------------ Bloom

    [Fact]
    public void Bloom_ZeroStrength_ReturnsUnchangedClone()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(250, 250, 250), 10, 10, 10, 10);

        using var result = FxService.Bloom(input, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Bloom_BrightRegionAboveThreshold_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(250, 250, 250), 10, 10, 10, 10);

        using var result = FxService.Bloom(input, 0.8, threshold: 200);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Bloom_NoPixelsAboveThreshold_LeavesImageNearlyUnchanged()
    {
        using var input = MakeUniform(20, 20, new Scalar(10, 10, 10));

        using var result = FxService.Bloom(input, 0.8, threshold: 250);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Bloom_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = FxService.Bloom(input, 0.5);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ LightLeak

    [Fact]
    public void LightLeak_ZeroStrength_ReturnsUnchangedClone()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = FxService.LightLeak(input, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void LightLeak_PositiveStrength_BrightensTopLeftMoreThanBottomRight()
    {
        using var input = MakeUniform(40, 40, new Scalar(50, 100, 150));

        using var result = FxService.LightLeak(input, 0.8);

        AssertChangesPixels(input, result);

        var topLeftDelta = Sum(result.Get<Vec3b>(0, 0)) - Sum(input.Get<Vec3b>(0, 0));
        var bottomRightDelta = Sum(result.Get<Vec3b>(39, 39)) - Sum(input.Get<Vec3b>(39, 39));
        Assert.True(topLeftDelta > bottomRightDelta);
    }

    [Fact]
    public void LightLeak_CustomColor_TintsTowardThatColor()
    {
        using var input = MakeUniform(20, 20, new Scalar(0, 0, 0));

        using var result = FxService.LightLeak(input, 1.0, new Vec3b(0, 0, 255));

        // Near the origin (max falloff), the strong red tint should dominate.
        var px = result.Get<Vec3b>(0, 0);
        Assert.True(px.Item2 > px.Item0);
    }

    [Fact]
    public void LightLeak_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = FxService.LightLeak(input, 0.5);

        AssertPreservesSizeAndType(input, result);
    }

    private static int Sum(Vec3b v) => v.Item0 + v.Item1 + v.Item2;

    // ------------------------------------------------------------------ ChromaticAberration

    [Fact]
    public void ChromaticAberration_ZeroStrength_ReturnsUnchangedClone()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.ChromaticAberration(input, 0.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void ChromaticAberration_PositiveStrength_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.ChromaticAberration(input, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(5.0)]
    public void ChromaticAberration_StrengthOutOfRange_IsClamped(double strength)
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.ChromaticAberration(input, strength);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void ChromaticAberration_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = FxService.ChromaticAberration(input, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void ChromaticAberration_PreservesGreenChannelUnshifted()
    {
        using var input = CreateTestInputWithRectangle(30, 30, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);

        using var result = FxService.ChromaticAberration(input, 1.0);

        var inputChannels = Cv2.Split(input);
        var resultChannels = Cv2.Split(result);
        try
        {
            // Green channel (index 1) is untouched by the radial remap.
            using var diff = new Mat();
            Cv2.Absdiff(inputChannels[1], resultChannels[1], diff);
            Assert.Equal(0, Cv2.CountNonZero(diff));
        }
        finally
        {
            foreach (var ch in inputChannels) ch.Dispose();
            foreach (var ch in resultChannels) ch.Dispose();
        }
    }

    // ------------------------------------------------------------------ Bokeh

    [Fact]
    public void Bokeh_ZeroCount_ReturnsUnchangedClone()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 0, 5.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Bokeh_PositiveCount_ChangesPixels()
    {
        using var input = MakeUniform(50, 50, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 30, 6.0);

        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    [Fact]
    public void Bokeh_NegativeCount_ClampedToZero_ReturnsUnchangedClone()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, -5, 5.0);

        AssertNoChange(input, result);
    }

    [Fact]
    public void Bokeh_CountAboveMax_IsClamped_DoesNotThrow()
    {
        using var input = MakeUniform(30, 30, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 10000, 5.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Bokeh_NonPositiveSize_ClampedToOne_DoesNotThrow()
    {
        using var input = MakeUniform(30, 30, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 10, 0.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Bokeh_LargeSize_DoesNotThrow()
    {
        using var input = MakeUniform(30, 30, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 10, 500.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Bokeh_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(50, 100, 150));

        using var result = FxService.Bokeh(input, 5, 3.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Bokeh_IsDeterministicAcrossCalls()
    {
        using var input = MakeUniform(30, 30, new Scalar(50, 100, 150));

        using var result1 = FxService.Bokeh(input, 20, 5.0);
        using var result2 = FxService.Bokeh(input, 20, 5.0);

        AssertNoChange(result1, result2);
    }
}
