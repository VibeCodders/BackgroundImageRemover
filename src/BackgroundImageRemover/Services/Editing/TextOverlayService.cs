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
    public bool BackgroundPlate { get; init; }
    public Vec3b PlateColor { get; init; } = new(0, 0, 0);
    public double PlateOpacity { get; init; } = 0.5;
    public int PlatePadding { get; init; } = 10;
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

        var textSize = Cv2.GetTextSize(options.Text, HersheyFonts.HersheySimplex, scale, thickness, out int baseline);

        int platePad = options.BackgroundPlate ? options.PlatePadding : 0;
        int outlinePad = options.OutlineThickness * 2;
        int shadowPad = Math.Abs(options.ShadowOffset);
        int blockPad = platePad + outlinePad + shadowPad + 4;

        var blockSize = new Size(textSize.Width + 2 * blockPad, textSize.Height + 2 * blockPad);
        using var block = new Mat(blockSize, MatType.CV_8UC4, Scalar.All(0));
        var origin = new Point(blockPad, blockPad + textSize.Height);

        if (options.BackgroundPlate)
        {
            var plateRect = new Rect(
                Math.Max(0, blockPad - platePad),
                Math.Max(0, blockPad - platePad),
                Math.Max(1, block.Width - 2 * (blockPad - platePad)),
                Math.Max(1, block.Height - 2 * (blockPad - platePad)));
            byte pa = (byte)Math.Round(255 * Math.Clamp(options.PlateOpacity, 0.0, 1.0));
            Cv2.Rectangle(block, plateRect, new Scalar(options.PlateColor.Item0, options.PlateColor.Item1, options.PlateColor.Item2, pa), -1);
        }

        if (options.ShadowOffset != 0)
        {
            byte sa = (byte)Math.Round(255 * Math.Clamp(options.ShadowOpacity, 0.0, 1.0));
            Cv2.PutText(block, options.Text,
                new Point(origin.X + options.ShadowOffset, origin.Y + options.ShadowOffset),
                HersheyFonts.HersheySimplex, scale, new Scalar(0, 0, 0, sa), thickness, LineTypes.AntiAlias);
        }

        if (options.OutlineThickness > 0)
        {
            Cv2.PutText(block, options.Text, origin, HersheyFonts.HersheySimplex, scale,
                new Scalar(options.OutlineColor.Item0, options.OutlineColor.Item1, options.OutlineColor.Item2, 255),
                thickness + 2 * options.OutlineThickness, LineTypes.AntiAlias);
        }

        Cv2.PutText(block, options.Text, origin, HersheyFonts.HersheySimplex, scale,
            new Scalar(options.Color.Item0, options.Color.Item1, options.Color.Item2, 255),
            thickness, LineTypes.AntiAlias);

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

    private static Point ComputeBlockOrigin(Size image, Size block, TextAnchor anchor, int margin)
    {
        int x = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.MiddleLeft or TextAnchor.BottomLeft => margin,
            TextAnchor.TopCenter or TextAnchor.Center or TextAnchor.BottomCenter => (image.Width - block.Width) / 2,
            _ => image.Width - block.Width - margin
        };

        int y = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.TopCenter or TextAnchor.TopRight => margin,
            TextAnchor.MiddleLeft or TextAnchor.Center or TextAnchor.MiddleRight => (image.Height - block.Height) / 2,
            _ => image.Height - block.Height - margin
        };

        return new Point(x, y);
    }

    /// <summary>Composites a BGRA text block onto a BGR image at the given offset, scaled by opacity.</summary>
    private static Mat CompositeTextBlock(Mat bgr, Mat blockBgra, Point position, double opacity)
    {
        var result = bgr.Clone();
        int x = Math.Clamp(position.X, 0, bgr.Width);
        int y = Math.Clamp(position.Y, 0, bgr.Height);
        int w = Math.Min(blockBgra.Width, bgr.Width - x);
        int h = Math.Min(blockBgra.Height, bgr.Height - y);
        if (w <= 0 || h <= 0)
        {
            return result;
        }

        using var blockRoi = new Mat(blockBgra, new Rect(0, 0, w, h));
        using var dstRoi = new Mat(result, new Rect(x, y, w, h));

        using var bsplit = ChannelSplit.Of(blockRoi);
        using var alpha = new Mat();
        bsplit[3].ConvertTo(alpha, MatType.CV_32FC1, opacity / 255.0);
        using var alpha3 = new Mat();
        Cv2.CvtColor(alpha, alpha3, ColorConversionCodes.GRAY2BGR);

        using var blockBgr = new Mat();
        Cv2.Merge(new[] { bsplit[0], bsplit[1], bsplit[2] }, blockBgr);
        using var dstF = new Mat();
        dstRoi.ConvertTo(dstF, MatType.CV_32FC3);
        using var blockF = new Mat();
        blockBgr.ConvertTo(blockF, MatType.CV_32FC3);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, inv);
        using var dstWeighted = dstF.Mul(inv).ToMat();
        using var blockWeighted = blockF.Mul(alpha3).ToMat();
        using var blended = (dstWeighted + blockWeighted).ToMat();
        using var outRoi = new Mat();
        blended.ConvertTo(outRoi, MatType.CV_8UC3);
        outRoi.CopyTo(dstRoi);

        return result;
    }
}
