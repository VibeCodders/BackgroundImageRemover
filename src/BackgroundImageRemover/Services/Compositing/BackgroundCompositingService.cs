using OpenCvSharp;

namespace BackgroundImageRemover.Services.Compositing;

/// <summary>Composites a BGRA cutout onto a solid color or another image, for non-transparent export.</summary>
public static class BackgroundCompositingService
{
    /// <summary>
    /// Returns a copy of <paramref name="bgra"/> cropped to the bounding box of its
    /// non-transparent (alpha &gt; 0) pixels, removing useless transparent margins.
    /// A fully transparent image is returned unchanged.
    /// </summary>
    public static Mat TrimTransparentBorders(Mat bgra)
    {
        var channels = Cv2.Split(bgra);
        try
        {
            using var nonZero = new Mat();
            Cv2.FindNonZero(channels[3], nonZero);
            if (nonZero.Rows == 0)
            {
                return bgra.Clone();
            }

            var bounds = Cv2.BoundingRect(nonZero);
            using var roi = new Mat(bgra, bounds);
            return roi.Clone();
        }
        finally
        {
            foreach (var c in channels) c.Dispose();
        }
    }

    public static Mat CompositeOntoColor(Mat bgra, Vec3b colorBgr)
    {
        using var background = new Mat(bgra.Size(), MatType.CV_8UC3, new Scalar(colorBgr.Item0, colorBgr.Item1, colorBgr.Item2));
        return CompositeOntoBgr(bgra, background);
    }

    public static Mat CompositeOntoImage(Mat bgra, Mat backgroundBgr)
    {
        using var resized = new Mat();
        Cv2.Resize(backgroundBgr, resized, bgra.Size(), interpolation: InterpolationFlags.Area);
        return CompositeOntoBgr(bgra, resized);
    }

    private static Mat CompositeOntoBgr(Mat bgra, Mat backgroundBgr)
    {
        var channels = Cv2.Split(bgra);
        try
        {
            using var alphaF = new Mat();
            channels[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

            using var foregroundBgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, foregroundBgr);

            using var alpha3 = new Mat();
            Cv2.CvtColor(alphaF, alpha3, ColorConversionCodes.GRAY2BGR);

            using var fgF = new Mat();
            foregroundBgr.ConvertTo(fgF, MatType.CV_32FC3);
            using var bgF = new Mat();
            backgroundBgr.ConvertTo(bgF, MatType.CV_32FC3);

            using var fgWeighted = fgF.Mul(alpha3).ToMat();
            using var oneMinusAlpha = new Mat();
            Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, oneMinusAlpha);
            using var bgWeighted = bgF.Mul(oneMinusAlpha).ToMat();

            using var blended = (fgWeighted + bgWeighted).ToMat();
            var result = new Mat();
            blended.ConvertTo(result, MatType.CV_8UC3);
            return result;
        }
        finally
        {
            foreach (var c in channels) c.Dispose();
        }
    }
}
