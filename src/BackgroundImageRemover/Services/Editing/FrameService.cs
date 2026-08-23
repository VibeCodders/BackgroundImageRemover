using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Frame and border effects on a BGRA image (border, rounded corners, transparent padding).
/// Every method returns a new Mat.
/// </summary>
public static class FrameService
{
    /// <summary>Adds a border of <paramref name="thickness"/> pixels around the image, optionally semi-transparent.</summary>
    public static Mat AddBorder(Mat bgra, int thickness, Vec3b color, double opacity = 1.0)
    {
        thickness = Math.Max(0, thickness);
        opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (thickness == 0)
        {
            return bgra.Clone();
        }

        byte a = ImageProcessingUtility.OpacityToAlphaByte(opacity);
        return ExpandCanvas(bgra, thickness, thickness, thickness, thickness,
            new Scalar(color.Item0, color.Item1, color.Item2, a));
    }

    /// <summary>Draws an accent line inside the image edge (an "inner border"), with adjustable opacity.</summary>
    public static Mat AddInnerBorder(Mat bgra, int thickness, Vec3b color, double opacity = 1.0)
    {
        thickness = Math.Max(1, thickness);
        opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (opacity <= 1e-4)
        {
            return bgra.Clone();
        }

        using var overlay = new Mat(bgra.Size(), MatType.CV_8UC4, Scalar.All(0));
        Cv2.Rectangle(overlay, new Rect(0, 0, bgra.Width, bgra.Height),
            new Scalar(color.Item0, color.Item1, color.Item2, 255), thickness, LineTypes.AntiAlias);
        return CompositeOverlay(bgra, overlay, opacity);
    }

    /// <summary>Renders a soft drop shadow under the whole (framed) image.</summary>
    public static Mat AddOuterShadow(Mat bgra, double offset, double blur, double opacity)
        => BackgroundCompositingService.ApplyDropShadow(bgra, offset, offset, blur, opacity);

    /// <summary>Rounds the alpha channel (and the color of the removed corners) to a transparent radius.</summary>
    public static Mat RoundCorners(Mat bgra, int radius)
    {
        radius = Math.Max(0, Math.Min(radius, Math.Min(bgra.Width, bgra.Height) / 2));
        if (radius == 0)
        {
            return bgra.Clone();
        }

        using var mask = new Mat(bgra.Size(), MatType.CV_8UC1, Scalar.All(0));

        // Two overlapping rectangles plus four corner discs build a filled rounded rectangle.
        Cv2.Rectangle(mask, new Rect(0, radius, bgra.Width, bgra.Height - 2 * radius), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Rect(radius, 0, bgra.Width - 2 * radius, bgra.Height), Scalar.All(255), -1);
        Cv2.Circle(mask, radius, radius, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, bgra.Width - radius - 1, radius, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, radius, bgra.Height - radius - 1, radius, Scalar.All(255), -1);
        Cv2.Circle(mask, bgra.Width - radius - 1, bgra.Height - radius - 1, radius, Scalar.All(255), -1);

        var result = bgra.Clone();
        using var inverted = new Mat();
        Cv2.BitwiseNot(mask, inverted);
        result.SetTo(new Scalar(0, 0, 0, 0), inverted);
        return result;
    }

    /// <summary>Expands the canvas by transparent margins (useful for adding breathing room around a cutout).</summary>
    public static Mat AddPadding(Mat bgra, int top, int right, int bottom, int left)
    {
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);
        left = Math.Max(0, left);
        if (top + right + bottom + left == 0)
        {
            return bgra.Clone();
        }

        return ExpandCanvas(bgra, top, right, bottom, left, Scalar.All(0));
    }

    /// <summary>Expands the canvas by margins filled with a mat color instead of transparency.</summary>
    public static Mat AddPaddingWithColor(Mat bgra, int top, int right, int bottom, int left, Vec3b matColor)
    {
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);
        left = Math.Max(0, left);
        if (top + right + bottom + left == 0)
        {
            return bgra.Clone();
        }

        return ExpandCanvas(bgra, top, right, bottom, left,
            new Scalar(matColor.Item0, matColor.Item1, matColor.Item2, 255));
    }

    /// <summary>Adds a border only on the requested sides, expanding the canvas on those sides.</summary>
    public static Mat AddPartialBorder(Mat bgra, int thickness, Vec3b color, double opacity, bool top, bool right, bool bottom, bool left)
    {
        thickness = Math.Max(0, thickness);
        if (thickness == 0)
        {
            return bgra.Clone();
        }

        int padL = left ? thickness : 0;
        int padT = top ? thickness : 0;
        int padR = right ? thickness : 0;
        int padB = bottom ? thickness : 0;
        byte a = ImageProcessingUtility.OpacityToAlphaByte(opacity);

        return ExpandCanvas(bgra, padT, padR, padB, padL,
            new Scalar(color.Item0, color.Item1, color.Item2, a));
    }

    /// <summary>Adds a border whose color runs a diagonal gradient from <paramref name="colorA"/> (top-left) to <paramref name="colorB"/> (bottom-right).</summary>
    public static Mat AddGradientBorder(Mat bgra, int thickness, Vec3b colorA, Vec3b colorB, double opacity = 1.0)
    {
        thickness = Math.Max(0, thickness);
        if (thickness == 0)
        {
            return bgra.Clone();
        }

        int w = bgra.Width + 2 * thickness;
        int h = bgra.Height + 2 * thickness;
        byte a = ImageProcessingUtility.OpacityToAlphaByte(opacity);
        var result = new Mat(h, w, MatType.CV_8UC4, Scalar.All(0));

        double denom = Math.Max(1, w + h - 2);
        Vec4b[] data = PixelLoop.GetData<Vec4b>(result);
        for (int i = 0; i < data.Length; i++)
        {
            double t = (double)(i % w + i / w) / denom;
            data[i] = new Vec4b(
                (byte)Math.Round(colorA.Item0 + (colorB.Item0 - colorA.Item0) * t),
                (byte)Math.Round(colorA.Item1 + (colorB.Item1 - colorA.Item1) * t),
                (byte)Math.Round(colorA.Item2 + (colorB.Item2 - colorA.Item2) * t),
                a);
        }
        PixelLoop.SetData(result, data);

        PasteInner(result, bgra, thickness, thickness);
        return result;
    }

    /// <summary>Draws a 3D bevel on the image edge (highlight on top/left, shadow on bottom/right).</summary>
    public static Mat AddBevel(Mat bgra, int thickness, Vec3b highlight, Vec3b shadow, double opacity = 1.0)
    {
        thickness = Math.Max(0, thickness);
        opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (thickness == 0 || opacity <= 1e-4)
        {
            return bgra.Clone();
        }

        using var overlay = new Mat(bgra.Size(), MatType.CV_8UC4, Scalar.All(0));
        byte a = ImageProcessingUtility.OpacityToAlphaByte(opacity);
        var highlightScalar = new Scalar(highlight.Item0, highlight.Item1, highlight.Item2, a);
        var shadowScalar = new Scalar(shadow.Item0, shadow.Item1, shadow.Item2, a);

        for (int i = 0; i < thickness; i++)
        {
            Cv2.Line(overlay, new Point(i, i), new Point(bgra.Width - 1 - i, i), highlightScalar, 1);
            Cv2.Line(overlay, new Point(i, i), new Point(i, bgra.Height - 1 - i), highlightScalar, 1);
            Cv2.Line(overlay, new Point(i, bgra.Height - 1 - i), new Point(bgra.Width - 1 - i, bgra.Height - 1 - i), shadowScalar, 1);
            Cv2.Line(overlay, new Point(bgra.Width - 1 - i, i), new Point(bgra.Width - 1 - i, bgra.Height - 1 - i), shadowScalar, 1);
        }

        return CompositeOverlay(bgra, overlay, 1.0);
    }

    /// <summary>Adds a bottom caption bar (polaroid style) of the given height and color.</summary>
    public static Mat AddPolaroidBar(Mat bgra, int height, Vec3b color, double opacity = 1.0)
    {
        height = Math.Max(0, height);
        if (height == 0)
        {
            return bgra.Clone();
        }

        byte a = ImageProcessingUtility.OpacityToAlphaByte(opacity);
        return ExpandCanvas(bgra, 0, 0, height, 0, new Scalar(color.Item0, color.Item1, color.Item2, a));
    }

    /// <summary>Fades the image corners toward <paramref name="color"/> (a photographic vignette).</summary>
    public static Mat AddVignette(Mat bgra, double strength, Vec3b color)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (strength <= 1e-4)
        {
            return bgra.Clone();
        }

        using var factor = new Mat(bgra.Size(), MatType.CV_32FC1);
        double cx = (bgra.Width - 1) / 2.0;
        double cy = (bgra.Height - 1) / 2.0;
        double maxDist = Math.Max(1e-6, Math.Sqrt(cx * cx + cy * cy));
        int w = bgra.Width;
        float[] factorData = PixelLoop.GetData<float>(factor);
        for (int i = 0; i < factorData.Length; i++)
        {
            int x = i % w;
            int y = i / w;
            double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxDist;
            double falloff = Math.Clamp((d - 0.35) / 0.65, 0.0, 1.0);
            factorData[i] = (float)(strength * falloff * falloff);
        }
        PixelLoop.SetData(factor, factorData);

        using var split = ChannelSplit.Of(bgra);
        // Channel values are on the 0..255 scale (matching chF/baseW below), so the target
        // color must NOT be normalized to 0..1 here or the vignette color barely registers.
        var colorValues = new[] { (double)color.Item0, (double)color.Item1, (double)color.Item2 };
        var channels = new Mat[4];
        try
        {
            using var invFactor = new Mat();
            Cv2.Subtract(new Mat(factor.Size(), factor.Type(), Scalar.All(1.0)), factor, invFactor);

            for (int i = 0; i < 3; i++)
            {
                using var chF = new Mat();
                split[i].ConvertTo(chF, MatType.CV_32FC1);
                using var baseW = chF.Mul(invFactor).ToMat();
                using var colorW = new Mat();
                Cv2.Multiply(factor, Scalar.All(colorValues[i]), colorW);
                channels[i] = (baseW + colorW).ToMat();
            }

            channels[3] = new Mat();
            split[3].ConvertTo(channels[3], MatType.CV_32FC1);
            var merged = new Mat();
            Cv2.Merge(channels, merged);
            using (merged)
            {
                var result = new Mat();
                merged.ConvertTo(result, MatType.CV_8UC4);
                return result;
            }
        }
        finally
        {
            foreach (var ch in channels) ch?.Dispose();
        }
    }

    /// <summary>Alpha-composites a colored overlay (using its alpha) over a BGRA image, scaled by <paramref name="opacity"/>.</summary>
    private static Mat CompositeOverlay(Mat baseBgra, Mat overlayBgra, double opacity)
    {
        return ImageProcessingUtility.CompositeOverBgra(baseBgra, overlayBgra, opacity);
    }

    /// <summary>Creates a <paramref name="fill"/>-colored canvas expanded by the given per-side
    /// margins and pastes <paramref name="bgra"/> at its original position.</summary>
    private static Mat ExpandCanvas(Mat bgra, int top, int right, int bottom, int left, Scalar fill)
    {
        var result = new Mat(bgra.Height + top + bottom, bgra.Width + left + right, MatType.CV_8UC4, fill);
        PasteInner(result, bgra, left, top);
        return result;
    }

    /// <summary>Copies <paramref name="src"/> into <paramref name="dest"/> at the given offset.</summary>
    private static void PasteInner(Mat dest, Mat src, int left, int top)
    {
        using var inner = new Mat(dest, new Rect(left, top, src.Width, src.Height));
        src.CopyTo(inner);
    }
}
