using BackgroundImageRemover.Helpers;
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
            FilterKind.Grayscale => GrayscaleService.ToGrayscale(inputBgr, 1.0),
            FilterKind.Sepia => ToSepia(inputBgr),
            FilterKind.Invert => LevelsService.Invert(inputBgr),
            FilterKind.Posterize => Posterize(inputBgr, posterizeLevels),
            FilterKind.Emboss => Emboss(inputBgr),
            FilterKind.Sketch => Sketch(inputBgr),
            FilterKind.Neon => Neon(inputBgr),
            FilterKind.Hdr => Hdr(inputBgr),
            FilterKind.Pencil => Pencil(inputBgr),
            FilterKind.Dreamy => Dreamy(inputBgr),
            FilterKind.Cartoon => Cartoon(inputBgr),
            FilterKind.Vivid => Vivid(inputBgr),
            FilterKind.Vintage => Vintage(inputBgr),
            FilterKind.Cool => Cool(inputBgr),
            FilterKind.Warm => Warm(inputBgr),
            FilterKind.Noir => Noir(inputBgr),
            FilterKind.Duotone => Duotone(inputBgr),
            FilterKind.Solarize => Solarize(inputBgr),
            _ => inputBgr.Clone()
        };

        return Blend(inputBgr, filtered, intensity);
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

    private static Mat Posterize(Mat input, int levels)
    {
        using var lutMat = ImageProcessingUtility.BuildPosterizeLut(levels);
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

    /// <summary>Neon edge-glow: white edge lines on black with a soft halo.</summary>
    private static Mat Neon(Mat input)
    {
        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);
        using var edgesBgr = new Mat();
        Cv2.CvtColor(edges, edgesBgr, ColorConversionCodes.GRAY2BGR);
        using var glow = new Mat();
        Cv2.GaussianBlur(edgesBgr, glow, new Size(0, 0), 5);
        var result = new Mat();
        Cv2.Add(edgesBgr, glow, result);
        return result;
    }

    /// <summary>HDR-style local detail enhancement.</summary>
    private static Mat Hdr(Mat input)
    {
        var result = new Mat();
        Cv2.DetailEnhance(input, result, 10f, 0.3f);
        return result;
    }

    /// <summary>Colored pencil-sketch rendition.</summary>
    private static Mat Pencil(Mat input)
    {
        using var grayPencil = new Mat();
        var colorPencil = new Mat();
        Cv2.PencilSketch(input, grayPencil, colorPencil, 60f, 0.07f, 0.02f);
        return colorPencil;
    }

    /// <summary>Dreamy Orton glow: a blurred, brightened copy blended with the sharp original.</summary>
    private static Mat Dreamy(Mat input)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(input, blurred, new Size(0, 0), 12);
        var result = new Mat();
        Cv2.AddWeighted(blurred, 0.6, input, 0.4, 20, result);
        return result;
    }

    /// <summary>Cartoon stylization (edge-preserving smoothing with flattened color).</summary>
    private static Mat Cartoon(Mat input)
    {
        var result = new Mat();
        Cv2.Stylization(input, result, 60f, 0.45f);
        return result;
    }

    /// <summary>Vivid: boosted saturation.</summary>
    private static Mat Vivid(Mat input)
    {
        return ImageProcessingUtility.AdjustSaturation(input, 0.5);
    }

    /// <summary>Vintage: sepia with reduced contrast.</summary>
    private static Mat Vintage(Mat input)
    {
        using var sepia = ToSepia(input);
        var result = new Mat();
        Cv2.AddWeighted(sepia, 1.0, input, 0.0, -10, result);
        return result;
    }

    /// <summary>Cool: blue-shifted color balance.</summary>
    private static Mat Cool(Mat input)
    {
        var result = input.Clone();
        var channels = Cv2.Split(result);
        try
        {
            Cv2.Add(channels[0], Scalar.All(20), channels[0]);  // blue up
            Cv2.Subtract(channels[2], Scalar.All(20), channels[2]); // red down
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>Warm: amber-shifted color balance.</summary>
    private static Mat Warm(Mat input)
    {
        var result = input.Clone();
        var channels = Cv2.Split(result);
        try
        {
            Cv2.Add(channels[2], Scalar.All(20), channels[2]);  // red up
            Cv2.Subtract(channels[0], Scalar.All(20), channels[0]); // blue down
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>Noir: high-contrast black and white.</summary>
    private static Mat Noir(Mat input)
    {
        using var grayBgr = input.ToGrayBgr();
        var result = new Mat();
        grayBgr.ConvertTo(result, MatType.CV_8UC3, 1.5, -30);
        return result;
    }

    /// <summary>Duotone: maps luminance onto a two-color gradient (deep indigo shadows to warm
    /// amber highlights), a classic poster/print look.</summary>
    private static Mat Duotone(Mat input)
    {
        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        using var grayF = new Mat();
        gray.ConvertTo(grayF, MatType.CV_32FC1, 1.0 / 255.0);
        using var grayBgrF = new Mat();
        Cv2.CvtColor(grayF, grayBgrF, ColorConversionCodes.GRAY2BGR);

        var shadow = new Scalar(80, 20, 10);
        var highlight = new Scalar(60, 200, 255);
        var diff = new Scalar(highlight.Val0 - shadow.Val0, highlight.Val1 - shadow.Val1, highlight.Val2 - shadow.Val2);

        using var diffMat = new Mat(grayBgrF.Size(), MatType.CV_32FC3, diff);
        using var scaled = new Mat();
        Cv2.Multiply(grayBgrF, diffMat, scaled);
        using var shadowMat = new Mat(grayBgrF.Size(), MatType.CV_32FC3, shadow);
        using var sumF = new Mat();
        Cv2.Add(scaled, shadowMat, sumF);

        var result = new Mat();
        sumF.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }

    /// <summary>Solarize: partial tone inversion above the midpoint (the classic darkroom effect).</summary>
    private static Mat Solarize(Mat input)
    {
        using var lut = new Mat(1, 256, MatType.CV_8UC1);
        var values = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            values[i] = i > 128 ? (byte)(255 - i) : (byte)i;
        }
        lut.SetArray(values);

        var result = new Mat();
        Cv2.LUT(input, lut, result);
        return result;
    }

    private static Mat Blend(Mat original, Mat filtered, double intensity)
    {
        return ImageProcessingUtility.BlendLinear(original, filtered, intensity);
    }
}
