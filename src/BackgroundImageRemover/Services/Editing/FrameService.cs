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

        byte a = (byte)Math.Round(255 * opacity);
        var result = new Mat(
            bgra.Height + 2 * thickness,
            bgra.Width + 2 * thickness,
            MatType.CV_8UC4,
            new Scalar(color.Item0, color.Item1, color.Item2, a));

        using var inner = new Mat(result, new Rect(thickness, thickness, bgra.Width, bgra.Height));
        bgra.CopyTo(inner);
        return result;
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

        var result = new Mat(
            bgra.Height + top + bottom,
            bgra.Width + left + right,
            MatType.CV_8UC4,
            Scalar.All(0));

        using var inner = new Mat(result, new Rect(left, top, bgra.Width, bgra.Height));
        bgra.CopyTo(inner);
        return result;
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

        var result = new Mat(
            bgra.Height + top + bottom,
            bgra.Width + left + right,
            MatType.CV_8UC4,
            new Scalar(matColor.Item0, matColor.Item1, matColor.Item2, 255));

        using var inner = new Mat(result, new Rect(left, top, bgra.Width, bgra.Height));
        bgra.CopyTo(inner);
        return result;
    }

    /// <summary>Alpha-composites a colored overlay (using its alpha) over a BGRA image, scaled by <paramref name="opacity"/>.</summary>
    private static Mat CompositeOverlay(Mat baseBgra, Mat overlayBgra, double opacity)
    {
        using var bsplit = ChannelSplit.Of(baseBgra);
        using var osplit = ChannelSplit.Of(overlayBgra);
        using var a = new Mat();
        osplit[3].ConvertTo(a, MatType.CV_32FC1, opacity / 255.0);

        var channels = new Mat[4];
        try
        {
            for (int i = 0; i < 3; i++)
            {
                using var baseF = new Mat();
                bsplit[i].ConvertTo(baseF, MatType.CV_32FC1);
                using var overF = new Mat();
                osplit[i].ConvertTo(overF, MatType.CV_32FC1);
                using var inv = new Mat();
                Cv2.Subtract(new Mat(a.Size(), a.Type(), Scalar.All(1.0)), a, inv);
                using var baseWeighted = baseF.Mul(inv).ToMat();
                using var overWeighted = overF.Mul(a).ToMat();
                channels[i] = (baseWeighted + overWeighted).ToMat();
            }

            channels[3] = new Mat();
            bsplit[3].ConvertTo(channels[3], MatType.CV_32FC1);
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
}
