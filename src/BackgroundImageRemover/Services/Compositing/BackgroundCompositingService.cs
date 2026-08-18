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

    /// <summary>
    /// Composites the cutout onto a Gaussian-blurred copy of the original photo ("portrait mode").
    /// The original is blurred at its native resolution before being resized to match the cutout.
    /// </summary>
    public static Mat CompositeOntoBlurredImage(Mat bgra, Mat originalBgr, double blurSigma)
    {
        using var blurred = new Mat();
        if (blurSigma <= 0)
        {
            originalBgr.CopyTo(blurred);
        }
        else
        {
            int kernel = Math.Max(1, (int)Math.Round(blurSigma * 3) * 2 + 1);
            Cv2.GaussianBlur(originalBgr, blurred, new Size(kernel, kernel), blurSigma, blurSigma);
        }
        return CompositeOntoImage(bgra, blurred);
    }

    /// <summary>Composites the cutout onto a vertical linear gradient between two BGR colors.</summary>
    public static Mat CompositeOntoGradient(Mat bgra, Vec3b topColorBgr, Vec3b bottomColorBgr)
    {
        using var column = new Mat(bgra.Height, 1, MatType.CV_8UC3);
        for (int y = 0; y < bgra.Height; y++)
        {
            double t = bgra.Height <= 1 ? 0 : (double)y / (bgra.Height - 1);
            var color = new Vec3b(
                (byte)Math.Round(topColorBgr.Item0 + (bottomColorBgr.Item0 - topColorBgr.Item0) * t),
                (byte)Math.Round(topColorBgr.Item1 + (bottomColorBgr.Item1 - topColorBgr.Item1) * t),
                (byte)Math.Round(topColorBgr.Item2 + (bottomColorBgr.Item2 - topColorBgr.Item2) * t));
            column.Set(y, 0, color);
        }

        using var gradient = new Mat();
        Cv2.Resize(column, gradient, bgra.Size(), interpolation: InterpolationFlags.Linear);
        return CompositeOntoBgr(bgra, gradient);
    }

    /// <summary>
    /// Renders a soft drop shadow under the cutout and returns a new, padded BGRA where the
    /// background stays transparent and the shadow is baked into the alpha channel. The subject
    /// is placed at the center of the padding; the shadow is offset by <paramref name="offsetX"/>
    /// (positive = right) and <paramref name="offsetY"/> (positive = down) and softened by
    /// <paramref name="blurSigma"/>. <paramref name="opacity"/> scales the shadow's alpha (0..1).
    /// </summary>
    public static Mat ApplyDropShadow(Mat bgra, double offsetX, double offsetY, double blurSigma, double opacity, Vec3b? shadowColor = null)
    {
        blurSigma = Math.Max(0, blurSigma);
        opacity = Math.Clamp(opacity, 0.0, 1.0);

        int padX = (int)Math.Ceiling(Math.Abs(offsetX) + 3 * blurSigma + 1);
        int padY = (int)Math.Ceiling(Math.Abs(offsetY) + 3 * blurSigma + 1);
        var outSize = new Size(bgra.Width + 2 * padX, bgra.Height + 2 * padY);

        // Subject silhouette as a float alpha (0..1).
        using var split = ChannelSplit.Of(bgra);
        using var alphaF = new Mat();
        split[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

        // Shadow alpha: the silhouette translated by the offset and softened.
        using var shadowA = new Mat(outSize, MatType.CV_32FC1, Scalar.All(0));
        using (var translate = new Mat(2, 3, MatType.CV_32FC1))
        {
            translate.Set(0, 0, 1f);
            translate.Set(0, 1, 0f);
            translate.Set(0, 2, (float)(padX + offsetX));
            translate.Set(1, 0, 0f);
            translate.Set(1, 1, 1f);
            translate.Set(1, 2, (float)(padY + offsetY));
            Cv2.WarpAffine(alphaF, shadowA, translate, outSize, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        }
        if (blurSigma > 0)
        {
            Cv2.GaussianBlur(shadowA, shadowA, new Size(0, 0), blurSigma, blurSigma);
        }
        if (opacity < 1)
        {
            Cv2.Multiply(shadowA, Scalar.All(opacity), shadowA);
        }

        // Subject placed at the padding offset on a float canvas.
        using var fgF = new Mat(outSize, MatType.CV_32FC4, Scalar.All(0));
        using var bgraF = new Mat();
        bgra.ConvertTo(bgraF, MatType.CV_32FC4, 1.0 / 255.0);
        using var fgRoi = new Mat(fgF, new Rect(padX, padY, bgra.Width, bgra.Height));
        bgraF.CopyTo(fgRoi);

        using var fgSplit = ChannelSplit.Of(fgF);
        var aFg = fgSplit[3];

        // Over-composite the shadow under the subject. outA = aFg + shadowA * (1 - aFg);
        // the shadow contributes its color scaled by the alpha it adds.
        using var oneMinusFg = new Mat();
        Cv2.Subtract(new Mat(outSize, MatType.CV_32FC1, Scalar.All(1.0)), aFg, oneMinusFg);
        using var shadowContrib = shadowA.Mul(oneMinusFg).ToMat();
        using var outA = new Mat();
        Cv2.Add(aFg, shadowContrib, outA);

        using var fgColor = new Mat();
        Cv2.Merge(new[] { fgSplit[0], fgSplit[1], fgSplit[2] }, fgColor);
        using var aFg3 = new Mat();
        Cv2.CvtColor(aFg, aFg3, ColorConversionCodes.GRAY2BGR);
        using var fgPremul = fgColor.Mul(aFg3).ToMat();

        using var shadowContrib3 = new Mat();
        Cv2.CvtColor(shadowContrib, shadowContrib3, ColorConversionCodes.GRAY2BGR);
        var sc = shadowColor ?? new Vec3b(0, 0, 0);
        using var shadowColorFloat = new Mat(outSize, MatType.CV_32FC3, new Scalar(sc.Item0 / 255.0, sc.Item1 / 255.0, sc.Item2 / 255.0));
        using var shadowPremul = shadowColorFloat.Mul(shadowContrib3).ToMat();
        using var numerator = (fgPremul + shadowPremul).ToMat();

        using var outA3 = new Mat();
        Cv2.CvtColor(outA, outA3, ColorConversionCodes.GRAY2BGR);
        using var epsilon = new Mat(outSize, MatType.CV_32FC3, Scalar.All(1e-6));
        Cv2.Max(outA3, epsilon, outA3); // guard division by zero
        using var outB = new Mat();
        Cv2.Divide(numerator, outA3, outB);

        using var outFloat = new Mat();
        Cv2.CvtColor(outB, outFloat, ColorConversionCodes.BGR2BGRA);
        using var outSplit = ChannelSplit.Of(outFloat);
        outA.CopyTo(outSplit[3]);
        Cv2.Merge(outSplit.Channels, outFloat);

        var result = new Mat();
        outFloat.ConvertTo(result, MatType.CV_8UC4, 255.0);
        return result;
    }

    /// <summary>
    /// Places the cutout on an expanded transparent canvas, offset from center. Padding adds
    /// breathing room and the offsets translate the subject (positive = right/down).
    /// </summary>
    public static Mat PlaceOnCanvas(Mat bgra, int padding, int offsetX, int offsetY)
    {
        padding = Math.Max(0, padding);
        if (padding == 0 && offsetX == 0 && offsetY == 0)
        {
            return bgra.Clone();
        }

        var outSize = new Size(bgra.Width + 2 * padding, bgra.Height + 2 * padding);
        var result = new Mat(outSize, MatType.CV_8UC4, Scalar.All(0));
        int x = Math.Clamp(padding + offsetX, 0, Math.Max(0, outSize.Width - bgra.Width));
        int y = Math.Clamp(padding + offsetY, 0, Math.Max(0, outSize.Height - bgra.Height));
        using var roi = new Mat(result, new Rect(x, y, bgra.Width, bgra.Height));
        bgra.CopyTo(roi);
        return result;
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
