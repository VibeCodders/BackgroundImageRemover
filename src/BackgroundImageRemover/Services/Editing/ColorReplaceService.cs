using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Replaces image pixels that fall within a color tolerance of a target color with a new color.
/// Used by the Color Replace tool. Matching is done in HSV space (with a circular hue distance),
/// and a softness parameter controls the width of the transition band around the tolerance edge.
/// </summary>
public static class ColorReplaceService
{
    /// <summary>
    /// Applies a color replacement to <paramref name="bgr"/>. <paramref name="tolerance"/> is
    /// 0..1 (1 = replace almost everything), <paramref name="softness"/> 0..1 controls the edge
    /// softness. When <paramref name="preserveLuminance"/> is true the original brightness is
    /// retained and only the color is swapped.
    /// </summary>
    public static Mat Apply(Mat bgr, Vec3b targetColor, Vec3b newColor, double tolerance, double softness, bool preserveLuminance)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        tolerance = Math.Clamp(tolerance, 0.0, 1.0);
        softness = Math.Clamp(softness, 0.0, 1.0);
        if (tolerance <= EditingGuard.Epsilon)
        {
            return bgr.Clone();
        }

        int w = bgr.Width;
        int h = bgr.Height;

        // Target color in HSV.
        using var targetMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(targetColor[0], targetColor[1], targetColor[2]));
        using var targetHsvMat = new Mat();
        Cv2.CvtColor(targetMat, targetHsvMat, ColorConversionCodes.BGR2HSV);
        Vec3b targetHsv = targetHsvMat.Get<Vec3b>(0, 0);
        double tHue = targetHsv[0], tSat = targetHsv[1], tVal = targetHsv[2];

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        int newV = Math.Max(newColor[0], Math.Max(newColor[1], newColor[2]));

        var result = new Mat(h, w, MatType.CV_8UC3);
        double edgeStart = tolerance * (1.0 - softness);

        try
        {
            // Bulk array access: one copy in/out instead of native interop per pixel.
            Vec3b[] hsvData = PixelLoop.GetData<Vec3b>(hsv);
            Vec3b[] srcData = PixelLoop.GetData<Vec3b>(bgr);
            var dstData = new Vec3b[srcData.Length];
            for (int i = 0; i < srcData.Length; i++)
            {
                Vec3b px = hsvData[i];
                double hueDist = Math.Abs(px[0] - tHue);
                hueDist = Math.Min(hueDist, 180 - hueDist);
                double sd = Math.Abs(px[1] - tSat) / 255.0;
                double vd = Math.Abs(px[2] - tVal) / 255.0;
                double d = Math.Sqrt((hueDist / 180.0) * (hueDist / 180.0) + sd * sd + vd * vd) / Math.Sqrt(3.0);

                double factor;
                if (d <= edgeStart)
                {
                    factor = 1.0;
                }
                else if (d >= tolerance)
                {
                    factor = 0.0;
                }
                else
                {
                    factor = softness <= EditingGuard.Epsilon ? 1.0 : (tolerance - d) / (tolerance - edgeStart);
                }

                Vec3b original = srcData[i];
                Vec3b replacement = newColor;
                if (preserveLuminance && newV > 0)
                {
                    double originalV = Math.Max(original[0], Math.Max(original[1], original[2]));
                    double scale = originalV / newV;
                    replacement = new Vec3b(
                        ClampByte(newColor[0] * scale),
                        ClampByte(newColor[1] * scale),
                        ClampByte(newColor[2] * scale));
                }

                dstData[i] = PixelColor.Blend(original, replacement, factor);
            }
            PixelLoop.SetData(result, dstData);

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static byte ClampByte(double value) => PixelColor.ClampByte(value);
}
