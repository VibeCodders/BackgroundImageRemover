using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class LevelsServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ Apply (default overload)

    [Fact]
    public void Apply_DefaultLevels_LeavesImageUnchanged()
    {
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = LevelsService.Apply(input, 0, 255, 1.0);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void Apply_BlackAndWhitePointClamp_PixelsBelowBlackBecomeBlack()
    {
        using var input = MakeUniform(4, 4, new Scalar(10, 10, 10));

        using var result = LevelsService.Apply(input, 50, 200, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(0, 0, 0), pixel);
    }

    [Fact]
    public void Apply_PixelsAboveWhitePoint_BecomeWhite()
    {
        using var input = MakeUniform(4, 4, new Scalar(250, 250, 250));

        using var result = LevelsService.Apply(input, 50, 200, 1.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(255, 255, 255), pixel);
    }

    [Fact]
    public void Apply_WhitePointClampedAboveBlackPoint_DoesNotThrow()
    {
        using var input = MakeUniform(4, 4, new Scalar(100, 100, 100));

        // whitePoint (10) <= blackPoint (200): the implementation must clamp whitePoint to
        // at least blackPoint + 1 instead of throwing or producing garbage.
        using var result = LevelsService.Apply(input, 200, 10, 1.0);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Apply_GammaClampedToValidRange_DoesNotThrow()
    {
        using var input = MakeUniform(4, 4, new Scalar(100, 150, 200));

        using var tooLow = LevelsService.Apply(input, 0, 255, -5.0);
        using var tooHigh = LevelsService.Apply(input, 0, 255, 1000.0);

        AssertPreservesSizeAndType(input, tooLow);
        AssertPreservesSizeAndType(input, tooHigh);
    }

    [Fact]
    public void Apply_GammaLessThanOne_DarkensMidtones()
    {
        using var input = MakeUniform(4, 4, new Scalar(128, 128, 128));

        using var result = LevelsService.Apply(input, 0, 255, 0.5);

        var pixel = result.Get<Vec3b>(0, 0);
        // gamma 0.5 -> exponent 1/gamma = 2, t^2 < t for t in (0,1), so output should be darker
        // than input at the midpoint.
        Assert.True(pixel.Item0 < 128);
    }

    [Fact]
    public void Apply_GammaGreaterThanOne_BrightensMidtones()
    {
        using var input = MakeUniform(4, 4, new Scalar(128, 128, 128));

        using var result = LevelsService.Apply(input, 0, 255, 2.0);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.True(pixel.Item0 > 128);
    }

    // ------------------------------------------------------------------ Apply (channel overload)

    [Theory]
    [InlineData(LevelsChannel.Red, 2)]
    [InlineData(LevelsChannel.Green, 1)]
    [InlineData(LevelsChannel.Blue, 0)]
    public void Apply_SingleChannel_OnlyAffectsThatChannel(LevelsChannel channel, int bgrIndex)
    {
        using var input = MakeUniform(4, 4, new Scalar(100, 100, 100));

        using var result = LevelsService.Apply(input, 50, 200, 1.0, channel, 0, 255);

        var pixel = result.Get<Vec3b>(0, 0);
        for (int i = 0; i < 3; i++)
        {
            if (i == bgrIndex)
            {
                Assert.NotEqual(100, pixel[i]);
            }
            else
            {
                Assert.Equal(100, pixel[i]);
            }
        }
    }

    [Fact]
    public void Apply_RgbChannel_AffectsAllChannels()
    {
        using var input = MakeUniform(4, 4, new Scalar(100, 100, 100));

        using var result = LevelsService.Apply(input, 50, 200, 1.0, LevelsChannel.Rgb, 0, 255);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.NotEqual(100, pixel.Item0);
        Assert.NotEqual(100, pixel.Item1);
        Assert.NotEqual(100, pixel.Item2);
    }

    [Fact]
    public void Apply_OutputRangeClamped_RespectsOutputBlackAndWhite()
    {
        using var input = MakeUniform(4, 4, new Scalar(250, 250, 250));

        using var result = LevelsService.Apply(input, 0, 255, 1.0, LevelsChannel.Rgb, 50, 200);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.True(pixel.Item0 <= 200);
    }

    [Fact]
    public void Apply_OutputWhiteClampedAboveOutputBlack_DoesNotThrow()
    {
        using var input = MakeUniform(4, 4, new Scalar(100, 100, 100));

        using var result = LevelsService.Apply(input, 0, 255, 1.0, LevelsChannel.Rgb, outputBlack: 200, outputWhite: 10);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ AutoLevels

    [Fact]
    public void AutoLevels_UniformImage_LeavesChannelsUnchanged()
    {
        // max - min < 1.0 per channel, so AutoLevels should leave a flat image untouched.
        using var input = MakeUniform(10, 10, new Scalar(50, 100, 150));

        using var result = LevelsService.AutoLevels(input);

        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }

    [Fact]
    public void AutoLevels_StretchesLowContrastImageToFullRange()
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(100, 100, 100), new Scalar(150, 150, 150), 2, 2, 4, 4);

        using var result = LevelsService.AutoLevels(input);

        Cv2.MinMaxLoc(result, out double min, out double max);
        Assert.True(min <= 5);
        Assert.True(max >= 250);
    }

    [Fact]
    public void AutoLevels_OnePixelImage_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(10, 20, 30));

        using var result = LevelsService.AutoLevels(input);

        AssertPreservesSizeAndType(input, result);
    }

    // ------------------------------------------------------------------ Equalize

    [Fact]
    public void Equalize_PreservesSizeAndType()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(30, 30, 30), new Scalar(220, 220, 220), 5, 5, 8, 8);

        using var result = LevelsService.Equalize(input);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void Equalize_LowContrastImage_ChangesPixels()
    {
        using var input = CreateTestInputWithRectangle(20, 20, new Scalar(100, 100, 100), new Scalar(120, 120, 120), 5, 5, 8, 8);

        using var result = LevelsService.Equalize(input);

        AssertChangesPixels(input, result);
    }

    // ------------------------------------------------------------------ Invert

    [Fact]
    public void Invert_ProducesNegative()
    {
        using var input = MakeUniform(4, 4, new Scalar(10, 100, 250));

        using var result = LevelsService.Invert(input);

        var pixel = result.Get<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(245, 155, 5), pixel);
    }

    [Fact]
    public void Invert_TwiceReturnsOriginal()
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(30, 60, 90), new Scalar(200, 150, 100), 2, 2, 4, 4);

        using var once = LevelsService.Invert(input);
        using var twice = LevelsService.Invert(once);

        AssertNoChange(input, twice);
    }

    // ------------------------------------------------------------------ AutoWhiteBalance

    [Fact]
    public void AutoWhiteBalance_PreservesSizeAndType()
    {
        using var input = CreateTestInputWithRectangle(10, 10, new Scalar(200, 50, 50), new Scalar(50, 200, 50), 2, 2, 4, 4);

        using var result = LevelsService.AutoWhiteBalance(input);

        AssertPreservesSizeAndType(input, result);
    }

    [Fact]
    public void AutoWhiteBalance_ColorCast_BalancesChannels()
    {
        // Strong blue cast: blue channel much brighter than the others.
        using var input = MakeUniform(10, 10, new Scalar(240, 40, 40));

        using var result = LevelsService.AutoWhiteBalance(input);

        var pixel = result.Get<Vec3b>(0, 0);
        // After balancing, the channel means should be closer together than in the input.
        int inputSpread = 240 - 40;
        int resultSpread = Math.Max(pixel.Item0, Math.Max(pixel.Item1, pixel.Item2))
            - Math.Min(pixel.Item0, Math.Min(pixel.Item1, pixel.Item2));
        Assert.True(resultSpread < inputSpread);
    }
}
