using OpenCvSharp;
using BackgroundImageRemover.Services.Compositing;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Renders emoji-style decorative marks (stars, hearts, sparkles, circles) onto a BGR image.</summary>
public static class EmojiOverlayService
{
    /// <summary>Available decorative emoji glyphs.</summary>
    public enum EmojiKind
    {
        Star,
        Heart,
        Sparkles,
        Circle,
        Diamond,
        Triangle,
        Boom,
        Peace,
        Arrow,
        Cross
    }

    /// <summary>
    /// Draws a decorative emoji at the given anchor position on the image.
    /// The emoji is scaled to <paramref name="size"/> pixels and blended with <paramref name="opacity"/>.
    /// </summary>
    public static Mat Render(Mat bgr, EmojiKind kind, Point position, int size, Vec3b color, double opacity)
    {
        size = Math.Max(8, size);
        opacity = Math.Clamp(opacity, 0.0, 1.0);

        // Create a transparent canvas for the emoji glyph.
        using var canvas = new Mat(size, size, MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
        DrawEmoji(canvas, kind, color, opacity);

        // Composite the emoji onto the image.
        var result = bgr.Clone();
        int x = position.X - size / 2;
        int y = position.Y - size / 2;
        int x0 = Math.Max(0, x);
        int y0 = Math.Max(0, y);
        int x1 = Math.Min(bgr.Width, x + size);
        int y1 = Math.Min(bgr.Height, y + size);

        if (x1 <= x0 || y1 <= y0)
        {
            return result;
        }

        int canvasX0 = x0 - x;
        int canvasY0 = y0 - y;
        using var emojiRoi = new Mat(canvas, new Rect(canvasX0, canvasY0, x1 - x0, y1 - y0));
        using var imgRoi = new Mat(result, new Rect(x0, y0, x1 - x0, y1 - y0));

        // The glyph's alpha channel already has `opacity` baked in (see DrawEmoji's brush
        // alpha = 255 * opacity), so only normalize to 0..1 here. Multiplying by `opacity`
        // again would apply it twice (effectively opacity^2), making a 0.5 opacity render
        // roughly as faint as 0.25.
        using var eSplit = ChannelSplit.Of(emojiRoi);
        using var alpha = new Mat();
        eSplit[3].ConvertTo(alpha, MatType.CV_32FC1, 1.0 / 255.0);
        using var alpha3 = new Mat();
        Cv2.CvtColor(alpha, alpha3, ColorConversionCodes.GRAY2BGR);

        using var eBgr = new Mat();
        Cv2.Merge(new[] { eSplit[0], eSplit[1], eSplit[2] }, eBgr);
        using var imgF = new Mat();
        imgRoi.ConvertTo(imgF, MatType.CV_32FC3);
        using var eF = new Mat();
        eBgr.ConvertTo(eF, MatType.CV_32FC3);

        using var inv = new Mat();
        Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, inv);
        using var imgWeighted = imgF.Mul(inv).ToMat();
        using var eWeighted = eF.Mul(alpha3).ToMat();
        using var blended = (imgWeighted + eWeighted).ToMat();
        using var outRoi = new Mat();
        blended.ConvertTo(outRoi, MatType.CV_8UC3);
        outRoi.CopyTo(imgRoi);

        return result;
    }

    /// <summary>Draws multiple emoji at random positions across the image for a festive overlay.</summary>
    public static Mat RenderScatter(Mat bgr, EmojiKind kind, int count, int minSize, int maxSize, Vec3b color, double opacity)
    {
        var result = bgr.Clone();
        var rand = new Random(42);
        for (int i = 0; i < count; i++)
        {
            int size = rand.Next(minSize, maxSize + 1);
            int x = rand.Next(0, bgr.Width);
            int y = rand.Next(0, bgr.Height);
            result = Render(result, kind, new Point(x, y), size, color, opacity);
        }
        return result;
    }

    private static void DrawEmoji(Mat canvas, EmojiKind kind, Vec3b color, double opacity)
    {
        var brush = new Scalar(color.Item0, color.Item1, color.Item2, (byte)(255 * opacity));
        var center = new Point(canvas.Width / 2, canvas.Height / 2);
        int half = canvas.Width / 2;

        switch (kind)
        {
            case EmojiKind.Star:
                DrawStar(canvas, center, half, half / 2, 5, brush);
                break;
            case EmojiKind.Heart:
                DrawHeart(canvas, center, half, brush);
                break;
            case EmojiKind.Sparkles:
                DrawSparkles(canvas, center, half, brush);
                break;
            case EmojiKind.Circle:
                Cv2.Circle(canvas, center, half, brush, -1, LineTypes.AntiAlias);
                break;
            case EmojiKind.Diamond:
                var pts = new[] {
                    new Point(center.X, center.Y - half),
                    new Point(center.X + half, center.Y),
                    new Point(center.X, center.Y + half),
                    new Point(center.X - half, center.Y)
                };
                Cv2.FillConvexPoly(canvas, pts, brush, LineTypes.AntiAlias);
                break;
            case EmojiKind.Triangle:
                var tpts = new[] {
                    new Point(center.X, center.Y - half),
                    new Point(center.X + half, center.Y + half),
                    new Point(center.X - half, center.Y + half)
                };
                Cv2.FillConvexPoly(canvas, tpts, brush, LineTypes.AntiAlias);
                break;
            case EmojiKind.Boom:
                Cv2.PutText(canvas, "!", center, HersheyFonts.HersheySimplex, half / 15.0, brush, 2, LineTypes.AntiAlias);
                DrawExplosion(canvas, center, half, brush);
                break;
            case EmojiKind.Peace:
                Cv2.Circle(canvas, center, half, brush, 2, LineTypes.AntiAlias);
                Cv2.Line(canvas, center, new Point(center.X, center.Y - half), brush, 2, LineTypes.AntiAlias);
                Cv2.Line(canvas, new Point(center.X, center.Y), new Point(center.X + half / 2, center.Y + half / 3), brush, 2, LineTypes.AntiAlias);
                Cv2.Line(canvas, new Point(center.X, center.Y + half / 3), new Point(center.X + half / 2, center.Y + half), brush, 2, LineTypes.AntiAlias);
                break;
            case EmojiKind.Arrow:
                Cv2.ArrowedLine(canvas, new Point(center.X - half, center.Y), new Point(center.X + half, center.Y), brush, 3, LineTypes.AntiAlias);
                break;
            case EmojiKind.Cross:
                Cv2.Line(canvas, new Point(center.X - half, center.Y - half), new Point(center.X + half, center.Y + half), brush, 3, LineTypes.AntiAlias);
                Cv2.Line(canvas, new Point(center.X - half, center.Y + half), new Point(center.X + half, center.Y - half), brush, 3, LineTypes.AntiAlias);
                break;
        }
    }

    private static void DrawStar(Mat img, Point center, int outerRadius, int innerRadius, int points, Scalar color)
    {
        var starPoints = new List<Point>();
        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI * i / points - Math.PI / 2;
            int r = i % 2 == 0 ? outerRadius : innerRadius;
            starPoints.Add(new Point(
                center.X + (int)Math.Round(r * Math.Cos(angle)),
                center.Y + (int)Math.Round(r * Math.Sin(angle))));
        }
        Cv2.FillConvexPoly(img, starPoints.ToArray(), color, LineTypes.AntiAlias);
    }

    private static void DrawHeart(Mat img, Point center, int size, Scalar color)
    {
        // Two circles + triangle
        var p1 = new Point(center.X - size / 2, center.Y);
        var p2 = new Point(center.X + size / 2, center.Y);
        Cv2.Circle(img, p1, size / 2, color, -1, LineTypes.AntiAlias);
        Cv2.Circle(img, p2, size / 2, color, -1, LineTypes.AntiAlias);
        var bottom = new Point(center.X, center.Y + size);
        var tri = new[] {
            new Point(center.X - size, center.Y + size / 2),
            new Point(center.X + size, center.Y + size / 2),
            bottom
        };
        Cv2.FillConvexPoly(img, tri, color, LineTypes.AntiAlias);
    }

    private static void DrawSparkles(Mat img, Point center, int size, Scalar color)
    {
        var rand = new Random(0);
        for (int i = 0; i < 8; i++)
        {
            double angle = Math.PI * 2 * i / 8;
            int r = size / 2;
            int x = center.X + (int)Math.Round(r * Math.Cos(angle) * 0.7);
            int y = center.Y + (int)Math.Round(r * Math.Sin(angle) * 0.7);
            int s = rand.Next(3, 8);
            Cv2.Circle(img, new Point(x, y), s, color, -1, LineTypes.AntiAlias);
        }
    }

    private static void DrawExplosion(Mat img, Point center, int size, Scalar color)
    {
        var rand = new Random(1);
        for (int i = 0; i < 12; i++)
        {
            double angle = Math.PI * 2 * i / 12;
            int r1 = size;
            int r2 = size + size / 2;
            int x1 = center.X + (int)Math.Round(r1 * Math.Cos(angle));
            int y1 = center.Y + (int)Math.Round(r1 * Math.Sin(angle));
            int x2 = center.X + (int)Math.Round(r2 * Math.Cos(angle));
            int y2 = center.Y + (int)Math.Round(r2 * Math.Sin(angle));
            Cv2.Line(img, new Point(x1, y1), new Point(x2, y2), color, 2, LineTypes.AntiAlias);
        }
    }
}
