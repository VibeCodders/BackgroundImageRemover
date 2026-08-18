using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Renders a text watermark onto a BGR image using OpenCV's built-in Hershey fonts.</summary>
public static class TextOverlayService
{
    public static Mat Render(
        Mat bgr,
        string? text,
        TextAnchor anchor,
        int fontSize,
        Vec3b color,
        double opacity,
        int margin)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return bgr.Clone();
        }

        fontSize = Math.Max(8, fontSize);
        margin = Math.Max(0, margin);
        opacity = Math.Clamp(opacity, 0.0, 1.0);

        const int BaseFontPx = 30;
        double scale = fontSize / (double)BaseFontPx;
        int thickness = Math.Max(1, (int)Math.Round(fontSize / 14.0));

        var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, thickness, out int baseline);

        var origin = ComputeOrigin(
            new Size(bgr.Width, bgr.Height),
            new Size(textSize.Width, textSize.Height),
            baseline,
            anchor,
            margin);

        using var textLayer = bgr.Clone();
        Cv2.PutText(
            textLayer,
            text,
            new Point(origin.X, origin.Y),
            HersheyFonts.HersheySimplex,
            scale,
            new Scalar(color.Item0, color.Item1, color.Item2),
            thickness,
            LineTypes.AntiAlias);

        if (opacity >= 0.999)
        {
            return textLayer.Clone();
        }

        var result = new Mat();
        Cv2.AddWeighted(bgr, 1.0 - opacity, textLayer, opacity, 0, result);
        return result;
    }

    private static Point ComputeOrigin(Size image, Size text, int baseline, TextAnchor anchor, int margin)
    {
        // Hershey glyphs are drawn with the origin at the text's bottom-left corner.
        int x = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.MiddleLeft or TextAnchor.BottomLeft => margin,
            TextAnchor.TopCenter or TextAnchor.Center or TextAnchor.BottomCenter => (image.Width - text.Width) / 2,
            _ => image.Width - text.Width - margin
        };

        int y = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.TopCenter or TextAnchor.TopRight => margin + text.Height,
            TextAnchor.MiddleLeft or TextAnchor.Center or TextAnchor.MiddleRight => (image.Height + text.Height) / 2,
            _ => image.Height - margin - baseline
        };

        return new Point(x, y);
    }
}
