using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

public class ThermalServiceTests
{
    [Fact]
    public void Apply_PreservesSizeAndType()
    {
        using var input = new Mat(10, 12, MatType.CV_8UC3, new Scalar(40, 90, 140));
        using var result = ThermalService.Apply(input, 1.0, false);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Apply_IntensityZero_IsGrayscale()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(60, 120, 200));
        using var result = ThermalService.Apply(input, 0, false);

        // Every pixel must have R == G == B.
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                var p = result.Get<Vec3b>(y, x);
                Assert.Equal(p.Item0, p.Item1);
                Assert.Equal(p.Item0, p.Item2);
            }
        }
    }

    [Fact]
    public void Apply_FullIntensity_ChangesPixels()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(60, 120, 200));
        using var result = ThermalService.Apply(input, 1.0, false);

        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_Invert_ChangesResult()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(60, 120, 200));
        using var normal = ThermalService.Apply(input, 1.0, false);
        using var inverted = ThermalService.Apply(input, 1.0, true);

        using var diff = new Mat();
        Cv2.Absdiff(normal, inverted, diff);
        using var diffGray = new Mat();
        Cv2.CvtColor(diff, diffGray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    [Fact]
    public void Apply_DarkAndBrightPixels_GetDifferentThermalColors()
    {
        using var input = new Mat(2, 2, MatType.CV_8UC3);
        input.Set<Vec3b>(0, 0, new Vec3b(10, 10, 10));   // very dark
        input.Set<Vec3b>(0, 1, new Vec3b(245, 245, 245)); // very bright

        using var result = ThermalService.Apply(input, 1.0, false);

        Vec3b dark = result.Get<Vec3b>(0, 0);
        Vec3b bright = result.Get<Vec3b>(0, 1);
        Assert.NotEqual(dark, bright);
    }
}
