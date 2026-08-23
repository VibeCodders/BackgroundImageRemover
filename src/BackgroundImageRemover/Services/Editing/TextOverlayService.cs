using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Options for rendering a text watermark overlay.</summary>
public sealed record TextOverlayOptions
{
    public string? Text { get; init; }
    public TextAnchor Anchor { get; init; } = TextAnchor.BottomRight;
    public int FontSize { get; init; } = 48;
    public Vec3b Color { get; init; } = new(255, 255, 255);
    public double Opacity { get; init; } = 1.0;
    public int Margin { get; init; } = 20;
    public double Rotation { get; init; }
    public int OutlineThickness { get; init; }
    public Vec3b OutlineColor { get; init; } = new(0, 0, 0);
    public bool Bold { get; init; }
    public int ShadowOffset { get; init; }
    public double ShadowOpacity { get; init; } = 0.5;
    public Vec3b ShadowColor { get; init; } = new(0, 0, 0);
    public double ShadowBlur { get; init; }
    public bool BackgroundPlate { get; init; }
    public Vec3b PlateColor { get; init; } = new(0, 0, 0);
    public double PlateOpacity { get; init; } = 0.5;
    public int PlatePadding { get; init; } = 10;
    public double LetterSpacing { get; init; }
    public int LineSpacing { get; init; }
    public bool AutoFitWidth { get; init; }
}

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
        return Render(bgr, new TextOverlayOptions
        {
            Text = text,
            Anchor = anchor,
            FontSize = fontSize,
            Color = color,
            Opacity = opacity,
            Margin = margin
        });
    }

    public static Mat Render(Mat bgr, TextOverlayOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Text))
        {
            return bgr.Clone();
        }

        int fontSize = Math.Max(8, options.FontSize);
        int margin = Math.Max(0, options.Margin);
        double opacity = Math.Clamp(options.Opacity, 0.0, 1.0);

        const int BaseFontPx = 30;
        double scale = fontSize / (double)BaseFontPx;
        int baseThickness = Math.Max(1, (int)Math.Round(fontSize / 14.0));
        int thickness = options.Bold ? baseThickness + 2 : baseThickness;

        var lines = options.Text.Replace("\r\n", "\n").Split('\n');
        double letterSpacing = Math.Max(0, options.LetterSpacing);
        int lineSpacing = Math.Max(0, options.LineSpacing);

        int platePad = options.BackgroundPlate ? options.PlatePadding : 0;
        int outlinePad = options.OutlineThickness * 2;
        int shadowPad = Math.Abs(options.ShadowOffset) + (int)Math.Ceiling(options.ShadowBlur * 3);
        int blockPad = platePad + outlinePad + shadowPad + 4;

        var measured = Measure(lines, scale, thickness, letterSpacing, lineSpacing);

        if (options.AutoFitWidth)
        {
            int available = Math.Max(1, bgr.Width - 2 * margin - 2 * blockPad);
            if (measured.MaxWidth > available)
            {
                double fit = (double)available / measured.MaxWidth;
                scale *= fit;
                thickness = Math.Max(1, (int)Math.Round(thickness * fit));
                measured = Measure(lines, scale, thickness, letterSpacing, lineSpacing);
            }
        }

        var blockSize = new Size(measured.MaxWidth + 2 * blockPad, measured.TotalHeight + 2 * blockPad);
        using var block = new Mat(blockSize, MatType.CV_8UC4, Scalar.All(0));
        var origin = new Point(blockPad, blockPad + measured.LineHeights[0]);

        if (options.BackgroundPlate)
        {
            var plateRect = new Rect(
                Math.Max(0, blockPad - platePad),
                Math.Max(0, blockPad - platePad),
                Math.Max(1, block.Width - 2 * (blockPad - platePad)),
                Math.Max(1, block.Height - 2 * (blockPad - platePad)));
            byte pa = ImageProcessingUtility.OpacityToAlphaByte(options.PlateOpacity);
            Cv2.Rectangle(block, plateRect, new Scalar(options.PlateColor.Item0, options.PlateColor.Item1, options.PlateColor.Item2, pa), -1);
        }

        if (options.ShadowOffset != 0)
        {
            byte sa = ImageProcessingUtility.OpacityToAlphaByte(options.ShadowOpacity);
            using var shadow = new Mat(blockSize, MatType.CV_8UC4, Scalar.All(0));
            var shadowOrigin = new Point(origin.X + options.ShadowOffset, origin.Y + options.ShadowOffset);
            var shadowColor = new Scalar(options.ShadowColor.Item0, options.ShadowColor.Item1, options.ShadowColor.Item2, sa);
            DrawTextContent(shadow, lines, measured.LineHeights, shadowOrigin, scale, thickness, shadowColor, letterSpacing, lineSpacing);

            if (options.ShadowBlur > 0)
            {
                using var ssplit = ChannelSplit.Of(shadow);
                int k = Math.Max(1, (int)Math.Round(options.ShadowBlur * 2) * 2 + 1);
                using var blurred = new Mat();
                Cv2.GaussianBlur(ssplit[3], blurred, new Size(k, k), options.ShadowBlur, options.ShadowBlur);
                blurred.CopyTo(ssplit[3]);
                Cv2.Merge(ssplit.Channels, shadow);
            }

            BlendOverBgra(block, shadow);
        }

        if (options.OutlineThickness > 0)
        {
            var outlineColor = new Scalar(options.OutlineColor.Item0, options.OutlineColor.Item1, options.OutlineColor.Item2, 255);
            DrawTextContent(block, lines, measured.LineHeights, origin, scale, thickness + 2 * options.OutlineThickness, outlineColor, letterSpacing, lineSpacing);
        }

        var mainColor = new Scalar(options.Color.Item0, options.Color.Item1, options.Color.Item2, 255);
        DrawTextContent(block, lines, measured.LineHeights, origin, scale, thickness, mainColor, letterSpacing, lineSpacing);

        Mat finalBlock;
        if (Math.Abs(options.Rotation) > 1e-4)
        {
            finalBlock = TransformService.Rotate(block, options.Rotation);
        }
        else
        {
            finalBlock = block.Clone();
        }

        using (finalBlock)
        {
            var position = ComputeBlockOrigin(new Size(bgr.Width, bgr.Height), new Size(finalBlock.Width, finalBlock.Height), options.Anchor, margin);
            return CompositeTextBlock(bgr, finalBlock, position, opacity);
        }
    }

    private static (int MaxWidth, int TotalHeight, IReadOnlyList<int> LineHeights) Measure(
        IReadOnlyList<string> lines, double scale, int thickness, double letterSpacing, int lineSpacing)
    {
        var heights = new List<int>(lines.Count);
        int maxWidth = 0;
        int totalHeight = 0;
        foreach (var line in lines)
        {
            var size = Cv2.GetTextSize(line, HersheyFonts.HersheySimplex, scale, thickness, out _);
            int width = size.Width + (int)Math.Round(letterSpacing * Math.Max(0, line.Length - 1));
            heights.Add(size.Height);
            maxWidth = Math.Max(maxWidth, width);
            totalHeight += size.Height;
        }
        totalHeight += (lines.Count - 1) * lineSpacing;
        return (maxWidth, totalHeight, heights);
    }

    private static void DrawTextContent(
        Mat dst,
        IReadOnlyList<string> lines,
        IReadOnlyList<int> lineHeights,
        Point firstOrigin,
        double scale,
        int thickness,
        Scalar color,
        double letterSpacing,
        int lineSpacing)
    {
        int y = firstOrigin.Y;
        for (int li = 0; li < lines.Count; li++)
        {
            string line = lines[li];
            if (letterSpacing <= 1e-6)
            {
                if (line.Length > 0)
                {
                    Cv2.PutText(dst, line, new Point(firstOrigin.X, y), HersheyFonts.HersheySimplex, scale, color, thickness, LineTypes.AntiAlias);
                }
            }
            else
            {
                int x = firstOrigin.X;
                foreach (char c in line)
                {
                    string s = c.ToString();
                    Cv2.PutText(dst, s, new Point(x, y), HersheyFonts.HersheySimplex, scale, color, thickness, LineTypes.AntiAlias);
                    x += Cv2.GetTextSize(s, HersheyFonts.HersheySimplex, scale, thickness, out _).Width + (int)Math.Round(letterSpacing);
                }
            }

            if (li < lines.Count - 1)
            {
                y += lineHeights[li] + lineSpacing;
            }
        }
    }

    /// <summary>Alpha-composites a BGRA layer over another BGRA layer (both premultiplied-style over blend).</summary>
    private static void BlendOverBgra(Mat bottom, Mat top)
    {
        using var bsplit = ChannelSplit.Of(bottom);
        using var tsplit = ChannelSplit.Of(top);
        using var ta = ImageProcessingUtility.Gray8ToFloat01(tsplit[3]);
        using var ones = new Mat(ta.Size(), ta.Type(), Scalar.All(1.0));
        using var inv = new Mat();
        Cv2.Subtract(ones, ta, inv);

        for (int i = 0; i < 3; i++)
        {
            using var bf = new Mat();
            bsplit[i].ConvertTo(bf, MatType.CV_32FC1);
            using var tf = new Mat();
            tsplit[i].ConvertTo(tf, MatType.CV_32FC1);
            using var bWeighted = bf.Mul(inv).ToMat();
            using var tWeighted = tf.Mul(ta).ToMat();
            using var sum = (bWeighted + tWeighted).ToMat();
            sum.ConvertTo(bsplit[i], MatType.CV_8UC1);
        }

        using var ba = ImageProcessingUtility.Gray8ToFloat01(bsplit[3]);
        using var baWeighted = ba.Mul(inv).ToMat();
        using var outA = (ta + baWeighted).ToMat();
        outA.ConvertTo(bsplit[3], MatType.CV_8UC1, 255.0);

        Cv2.Merge(bsplit.Channels, bottom);
    }

    private static Point ComputeBlockOrigin(Size image, Size block, TextAnchor anchor, int margin)
        => AnchorPositionHelper.ComputeOrigin(image, block, anchor, margin);

    /// <summary>Composites a BGRA text block onto a BGR image at the given offset, scaled by opacity.</summary>
    private static Mat CompositeTextBlock(Mat bgr, Mat blockBgra, Point position, double opacity)
    {
        var result = bgr.Clone();
        // position may be negative (e.g. a text block wider/taller than the image once margins
        // are subtracted). Clamping x/y to 0 without cropping the same amount off the block's
        // source origin used to paste the block's top-left corner at canvas (0,0) instead of the
        // correctly clipped portion of the block (mirrors the analogous fix in OverlayService).
        int destX = Math.Clamp(position.X, 0, bgr.Width);
        int destY = Math.Clamp(position.Y, 0, bgr.Height);
        int srcX = destX - position.X;
        int srcY = destY - position.Y;
        int w = Math.Min(blockBgra.Width - srcX, bgr.Width - destX);
        int h = Math.Min(blockBgra.Height - srcY, bgr.Height - destY);
        if (w <= 0 || h <= 0)
        {
            return result;
        }

        using var blockRoi = new Mat(blockBgra, new Rect(srcX, srcY, w, h));
        using var dstRoi = new Mat(result, new Rect(destX, destY, w, h));

        ImageProcessingUtility.AlphaComposite(dstRoi, blockRoi, opacity);

        return result;
    }
}
