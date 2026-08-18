using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Vectorized artistic filters on a BGR image. Each filter returns a new Mat and is blended
/// against the original by <c>intensity</c> (0 = no change, 1 = full effect).
/// </summary>
public static class FilterService
{
    public static Mat Apply(Mat inputBgr, FilterKind kind, double intensity, int posterizeLevels = 4)
    {
        ArgumentNullException.ThrowIfNull(inputBgr);

        using var filtered = kind switch
        {
            FilterKind.Grayscale => ToGrayscale(inputBgr),
            FilterKind.Sepia => ToSepia(inputBgr),
            FilterKind.Invert => Invert(inputBgr),
            FilterKind.Posterize => Posterize(inputBgr, posterizeLevels),
            FilterKind.Emboss => Emboss(inputBgr),
            FilterKind.Sketch => Sketch(inputBgr),
            _ => inputBgr.Clone()
        };

        return Blend(inputBgr, filtered, intensity);
    }

    private static Mat ToGrayscale(Mat input)
    {
        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        var result = new Mat();
        Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ToSepia(Mat input)
    {
        // Row-major matrix mapping the BGR input vector to a sepia BGR output vector.
        using var sepia = new Mat(3, 3, MatType.CV_32FC1);
        sepia.SetArray(new[]
        {
            0.131f, 0.534f, 0.272f,
            0.168f, 0.686f, 0.349f,
            0.189f, 0.769f, 0.393f
        });
        var result = new Mat();
        Cv2.Transform(input, result, sepia);
        return result;
    }

    private static Mat Invert(Mat input)
    {
        var result = new Mat();
        Cv2.BitwiseNot(input, result);
        return result;
    }

    private static Mat Posterize(Mat input, int levels)
    {
        int bucket = Math.Max(1, 256 / Math.Max(1, levels));
        var lut = new byte[256];
        for (int i = 0; i < lut.Length; i++)
        {
            lut[i] = (byte)((i / bucket) * bucket);
        }

        using var lutMat = new Mat(1, 256, MatType.CV_8UC1);
        lutMat.SetArray(lut);
        var result = new Mat();
        Cv2.LUT(input, lutMat, result);
        return result;
    }

    private static Mat Emboss(Mat input)
    {
        using var kernel = new Mat(3, 3, MatType.CV_32FC1);
        kernel.SetArray(new[]
        {
            -2f, -1f, 0f,
            -1f, 1f, 1f,
            0f, 1f, 2f
        });
        using var filtered = new Mat();
        Cv2.Filter2D(input, filtered, -1, kernel);
        var result = new Mat();
        Cv2.Add(filtered, Scalar.All(128), result);
        return result;
    }

    private static Mat Sketch(Mat input)
    {
        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);

        using var grayF = new Mat();
        gray.ConvertTo(grayF, MatType.CV_32FC1);

        // Inverted, blurred copy used as the dodge denominator.
        using var inv = new Mat();
        Cv2.BitwiseNot(gray, inv);
        using var invBlur = new Mat();
        Cv2.GaussianBlur(inv, invBlur, new Size(0, 0), 10);

        using var denom = new Mat();
        Cv2.Subtract(Scalar.All(255), invBlur, denom, mask: null, dtype: (int)MatType.CV_32FC1);
        using var denomClamped = new Mat();
        Cv2.Max(denom, Scalar.All(1.0), denomClamped);

        using var sketchF = new Mat();
        Cv2.Divide(grayF, denomClamped, sketchF, 255.0);
        using var sketch = new Mat();
        sketchF.ConvertTo(sketch, MatType.CV_8UC1);

        var result = new Mat();
        Cv2.CvtColor(sketch, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat Blend(Mat original, Mat filtered, double intensity)
    {
        if (intensity <= 0.001)
        {
            return original.Clone();
        }
        if (intensity >= 0.999)
        {
            return filtered.Clone();
        }

        var result = new Mat();
        Cv2.AddWeighted(original, 1.0 - intensity, filtered, intensity, 0, result);
        return result;
    }
}
