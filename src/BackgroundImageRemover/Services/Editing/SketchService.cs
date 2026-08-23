using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Pencil-sketch rendering (luminance / blurred luminance via color dodge).</summary>
public static class SketchService
{
    /// <summary>
    /// Converts <paramref name="bgr"/> into a pencil sketch: grayscale is divided by a blurred
    /// copy of itself (OpenCV's classic color-dodge sketch), with an optional negative inversion.
    /// The caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, int blurRadius, bool invert)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        var k = Math.Max(1, blurRadius);
        if (k % 2 == 0)
        {
            k++;
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(k, k), 0);

        // divide(gray, blurred, scale=256): white where the two match, dark pencil lines
        // where the blurred luminance is much brighter than the local pixel.
        using var sketch = new Mat();
        Cv2.Divide(gray, blurred, sketch, scale: 256, dtype: -1);

        if (invert)
        {
            Cv2.BitwiseNot(sketch, sketch);
        }

        var result = new Mat();
        Cv2.CvtColor(sketch, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }
}
