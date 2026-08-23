using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Base class for service unit tests that provides common test setup and assertion helpers.
/// Reduces the boilerplate code that was previously duplicated across every service test file.
/// </summary>
public abstract class ServiceTestBase
{
    /// <summary>
    /// Creates a test input Mat with the specified dimensions and fill color.
    /// </summary>
    /// <param name="width">Width of the image.</param>
    /// <param name="height">Height of the image.</param>
    /// <param name="fillColor">Color to fill the image with.</param>
    /// <returns>A new Mat with the specified dimensions and fill color.</returns>
    protected static Mat CreateTestInput(int width, int height, Scalar fillColor)
    {
        return new Mat(height, width, MatType.CV_8UC3, fillColor);
    }

    /// <summary>
    /// Creates a test input Mat with a rectangle drawn in a different color.
    /// Useful for testing effects that require edge detection or contrast.
    /// </summary>
    /// <param name="width">Width of the image.</param>
    /// <param name="height">Height of the image.</param>
    /// <param name="backgroundColor">Background color.</param>
    /// <param name="rectColor">Rectangle color.</param>
    /// <param name="rectX">X coordinate of the rectangle.</param>
    /// <param name="rectY">Y coordinate of the rectangle.</param>
    /// <param name="rectWidth">Width of the rectangle.</param>
    /// <param name="rectHeight">Height of the rectangle.</param>
    /// <returns>A new Mat with the specified rectangle.</returns>
    protected static Mat CreateTestInputWithRectangle(
        int width, int height,
        Scalar backgroundColor, Scalar rectColor,
        int rectX, int rectY, int rectWidth, int rectHeight)
    {
        var input = CreateTestInput(width, height, backgroundColor);
        Cv2.Rectangle(input, new Rect(rectX, rectY, rectWidth, rectHeight), rectColor, -1);
        return input;
    }

    /// <summary>
    /// Asserts that the result preserves the size and type of the input.
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="result">The result image.</param>
    protected static void AssertPreservesSizeAndType(Mat input, Mat result)
    {
        ServiceTestHelper.AssertPreservesSizeAndType(input, result);
    }

    /// <summary>
    /// Asserts that at least one pixel differs between input and result.
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="result">The result image.</param>
    protected static void AssertChangesPixels(Mat input, Mat result)
    {
        ServiceTestHelper.AssertChangesPixels(input, result);
    }

    /// <summary>
    /// Asserts that no pixel was modified (input and result are identical).
    /// </summary>
    /// <param name="input">The input image.</param>
    /// <param name="result">The result image.</param>
    protected static void AssertNoChange(Mat input, Mat result)
    {
        ServiceTestHelper.AssertNoChange(input, result);
    }

    /// <summary>
    /// Asserts that two results differ from each other.
    /// </summary>
    /// <param name="a">First result image.</param>
    /// <param name="b">Second result image.</param>
    protected static void AssertResultsDiffer(Mat a, Mat b)
    {
        ServiceTestHelper.AssertResultsDiffer(a, b);
    }

    /// <summary>
    /// Runs a test that verifies a service preserves size and type, then checks if pixels change.
    /// </summary>
    /// <param name="serviceFunc">The service function to test.</param>
    /// <param name="input">The input image.</param>
    protected void AssertServicePreservesSizeAndType(Func<Mat, Mat> serviceFunc, Mat input)
    {
        using var result = serviceFunc(input);
        AssertPreservesSizeAndType(input, result);
    }

    /// <summary>
    /// Runs a test that verifies a service preserves size and type, then checks if pixels change.
    /// </summary>
    /// <param name="serviceFunc">The service function to test.</param>
    /// <param name="input">The input image.</param>
    protected void AssertServiceChangesPixels(Func<Mat, Mat> serviceFunc, Mat input)
    {
        using var result = serviceFunc(input);
        AssertPreservesSizeAndType(input, result);
        AssertChangesPixels(input, result);
    }

    /// <summary>
    /// Runs a test that verifies a service preserves size and type, then checks if pixels change.
    /// </summary>
    /// <param name="serviceFunc">The service function to test.</param>
    /// <param name="input">The input image.</param>
    protected void AssertServiceNoChange(Func<Mat, Mat> serviceFunc, Mat input)
    {
        using var result = serviceFunc(input);
        AssertPreservesSizeAndType(input, result);
        AssertNoChange(input, result);
    }
}
