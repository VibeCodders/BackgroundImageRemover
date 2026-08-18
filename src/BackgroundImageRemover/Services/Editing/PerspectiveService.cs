using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Four-point perspective correction (keystone/straighten) for the Perspective tool.</summary>
public static class PerspectiveService
{
    /// <summary>
    /// Maps the source quadrangle (top-left, top-right, bottom-right, bottom-left) to a
    /// rectangle of <paramref name="outputWidth"/> x <paramref name="outputHeight"/> pixels.
    /// </summary>
    public static Mat Correct(
        Mat bgr,
        Point2f topLeft,
        Point2f topRight,
        Point2f bottomRight,
        Point2f bottomLeft,
        int outputWidth,
        int outputHeight,
        InterpolationFlags interpolation = InterpolationFlags.Linear)
    {
        outputWidth = Math.Max(1, outputWidth);
        outputHeight = Math.Max(1, outputHeight);

        var srcQuad = new[] { topLeft, topRight, bottomRight, bottomLeft };
        var dstQuad = new[]
        {
            new Point2f(0, 0),
            new Point2f(outputWidth - 1, 0),
            new Point2f(outputWidth - 1, outputHeight - 1),
            new Point2f(0, outputHeight - 1)
        };

        using var matrix = Cv2.GetPerspectiveTransform(srcQuad, dstQuad);
        var result = new Mat();
        Cv2.WarpPerspective(bgr, result, matrix, new Size(outputWidth, outputHeight), interpolation, BorderTypes.Constant, Scalar.All(0));
        return result;
    }

    /// <summary>Returns the four image corners, used as the initial (identity) quadrangle.</summary>
    public static (Point2f TopLeft, Point2f TopRight, Point2f BottomRight, Point2f BottomLeft) DefaultQuad(Size size)
        => (
            new Point2f(0, 0),
            new Point2f(size.Width - 1, 0),
            new Point2f(size.Width - 1, size.Height - 1),
            new Point2f(0, size.Height - 1));
}
