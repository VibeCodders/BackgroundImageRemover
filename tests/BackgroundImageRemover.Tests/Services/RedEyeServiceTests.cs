using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>Tests for the Red Eye removal tool.</summary>
public class RedEyeServiceTests
{
    private static Mat MakeImageWithRedEye(byte redValue = 220)
        => new(40, 40, MatType.CV_8UC3, new Scalar(200, 180, 160)); // skin-ish background

    [Fact]
    public void RemoveRedEyes_RedPixelInRadius_IsNeutralizedToGray()
    {
        using var image = MakeImageWithRedEye();
        // Red-dominant eye with some green/blue so the neutralization target is > 0.
        Cv2.Circle(image, new Point(20, 20), 6, new Scalar(120, 100, 220), -1);

        using var result = RedEyeService.RemoveRedEyes(image, new Point(20, 20), radius: 10);
        var pixel = result.Get<Vec3b>(20, 20);

        // The red channel was pulled down to the average of G/B: R == G == B (gray).
        Assert.Equal(pixel.Item0, pixel.Item2);
        Assert.Equal(pixel.Item1, pixel.Item2);
        Assert.InRange(pixel.Item2, 80, 200);
    }

    [Fact]
    public void RemoveRedEyes_DarkRedBelowThreshold_IsLeftAlone()
    {
        using var image = MakeImageWithRedEye(redValue: 50); // rv <= 80: not bright enough to be an eye
        Cv2.Circle(image, new Point(20, 20), 6, new Scalar(0, 0, 50), -1);

        using var result = RedEyeService.RemoveRedEyes(image, new Point(20, 20), radius: 10);

        Assert.Equal(new Vec3b(0, 0, 50), result.Get<Vec3b>(20, 20));
    }

    [Fact]
    public void RemoveRedEyes_PixelsOutsideRadius_AreUntouched()
    {
        using var image = MakeImageWithRedEye();
        Cv2.Circle(image, new Point(20, 20), 6, new Scalar(0, 0, 220), -1);

        using var result = RedEyeService.RemoveRedEyes(image, new Point(20, 20), radius: 3);

        // (20, 20) is inside the radius, (35, 35) is far outside and must stay background.
        Assert.NotEqual(new Vec3b(200, 180, 160), result.Get<Vec3b>(20, 20));
        Assert.Equal(new Vec3b(200, 180, 160), result.Get<Vec3b>(35, 35));
    }

    [Fact]
    public void RemoveRedEyes_NonRedPixel_IsLeftAlone()
    {
        using var image = MakeImageWithRedEye();
        Cv2.Circle(image, new Point(20, 20), 6, new Scalar(220, 0, 0), -1); // pure blue: not red-dominant

        using var result = RedEyeService.RemoveRedEyes(image, new Point(20, 20), radius: 10);

        Assert.Equal(new Vec3b(220, 0, 0), result.Get<Vec3b>(20, 20));
    }

    [Fact]
    public void RemoveRedEyes_NullOrEmpty_ReturnsEmptyWithoutThrowing()
    {
        using var result = RedEyeService.RemoveRedEyes(null!, new Point(20, 20), 10);

        Assert.True(result.Empty());

        using var empty = new Mat(0, 0, MatType.CV_8UC3);
        using var result2 = RedEyeService.RemoveRedEyes(empty, new Point(20, 20), 10);
        Assert.True(result2.Empty());
    }
}
