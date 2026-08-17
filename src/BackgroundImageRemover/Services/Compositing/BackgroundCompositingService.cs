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
        using var split = ChannelSplit.Of(bgra);
        using var nonZero = new Mat();
        Cv2.FindNonZero(split[3], nonZero);
        if (nonZero.Rows == 0)
        {
            return bgra.Clone();
        }

        var bounds = Cv2.BoundingRect(nonZero);
        using var roi = new Mat(bgra, bounds);
        return roi.Clone();
    }

    /// <summary>
    /// True when <paramref name="alpha"/> is non-null and actually contains transparency
    /// (some pixel below 255). A loaded PNG can carry a 4th channel that is uniformly opaque
    /// -- that's a plain photo saved in an RGBA container, not a real cutout.
    /// </summary>
    public static bool HasMeaningfulTransparency(Mat? alpha)
    {
        if (alpha is null)
        {
            return false;
        }

        Cv2.MinMaxLoc(alpha, out double min, out _);
        return min < 255;
    }

    /// <summary>
    /// Zeroes B/G/R at every pixel where alpha is exactly 0, in place. Fully-removed pixels
    /// must not carry the original color data forward: leaving it in place is invisible today,
    /// but re-running a strategy (or reopening the file) later reads it back as real image
    /// content and can resurrect the old background.
    /// </summary>
    public static void ZeroFullyTransparentPixels(Mat bgra)
    {
        using var split = ChannelSplit.Of(bgra);
        using var mask = new Mat();
        Cv2.Compare(split[3], 0, mask, CmpType.EQ);
        split[0].SetTo(Scalar.All(0), mask);
        split[1].SetTo(Scalar.All(0), mask);
        split[2].SetTo(Scalar.All(0), mask);
        Cv2.Merge(split.Channels, bgra);
    }

    /// <summary>Overwrites the alpha channel of <paramref name="bgra"/> with <paramref name="newAlpha"/>, in place.</summary>
    public static void ReplaceAlphaChannel(Mat bgra, Mat newAlpha)
    {
        using var split = ChannelSplit.Of(bgra);
        newAlpha.CopyTo(split[3]);
        Cv2.Merge(split.Channels, bgra);
    }

    /// <summary>Splits a BGRA Mat into an independent BGR Mat and an independent alpha Mat.</summary>
    public static (Mat Bgr, Mat Alpha) SplitBgra(Mat bgra)
    {
        using var split = ChannelSplit.Of(bgra);
        var bgr = new Mat();
        try
        {
            Cv2.Merge(new[] { split[0], split[1], split[2] }, bgr);
        }
        catch
        {
            bgr.Dispose();
            throw;
        }

        var alpha = split[3].Clone();
        return (bgr, alpha);
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
        using var split = ChannelSplit.Of(bgra);
        using var alphaF = new Mat();
        split[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

        using var foregroundBgr = new Mat();
        Cv2.Merge(new[] { split[0], split[1], split[2] }, foregroundBgr);

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
}
