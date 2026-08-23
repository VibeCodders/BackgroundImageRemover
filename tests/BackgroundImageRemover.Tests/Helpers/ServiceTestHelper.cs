using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Shared assertion helpers for service unit tests. Replaces the repeated
/// "preserve size/type", "pixels changed", "no change" and "uniform output"
/// patterns that were copy-pasted across every service test file.
/// </summary>
public static class ServiceTestHelper
{
    /// <summary>Asserts that the result has the same dimensions and Mat type as the input.</summary>
    public static void AssertPreservesSizeAndType(Mat input, Mat result)
    {
        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    /// <summary>
    /// Asserts that at least one pixel differs between input and result.
    /// Handles both BGR (3-channel) and grayscale (1-channel) images.
    /// </summary>
    public static void AssertChangesPixels(Mat input, Mat result)
    {
        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = ToSingleChannel(diff);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    /// <summary>
    /// Asserts that no pixel was modified (input and result are identical).
    /// </summary>
    public static void AssertNoChange(Mat input, Mat result)
    {
        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var diffGray = ToSingleChannel(diff);
        Assert.Equal(0, Cv2.CountNonZero(diffGray));
    }

    /// <summary>Asserts that every pixel in the result has the same color.</summary>
    public static void AssertAllPixelsSame(Mat result)
    {
        Vec3b first = result.Get<Vec3b>(0, 0);
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                Assert.Equal(first, result.Get<Vec3b>(y, x));
            }
        }
    }

    /// <summary>
    /// Asserts that two results differ from each other (useful for
    /// comparing the output of different parameter sets).
    /// </summary>
    public static void AssertResultsDiffer(Mat a, Mat b)
    {
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        using var diffGray = ToSingleChannel(diff);
        Assert.True(Cv2.CountNonZero(diffGray) > 0);
    }

    /// <summary>Converts any Mat to a single-channel Mat suitable for CountNonZero.</summary>
    private static Mat ToSingleChannel(Mat src)
    {
        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
