using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Type of vector shape drawn by the Shape tool.</summary>
public enum ShapeKind
{
    /// <summary>Axis-aligned rectangle.</summary>
    Rectangle,

    /// <summary>Axis-aligned ellipse.</summary>
    Ellipse,

    /// <summary>Straight segment from the shape start to end point.</summary>
    Line,

    /// <summary>Straight segment with an arrowhead at the end point.</summary>
    Arrow,

    /// <summary>Regular polygon with a configurable number of sides.</summary>
    Polygon,

    /// <summary>Star polygon with a configurable number of points and inner radius.</summary>
    Star
}

/// <summary>
/// Draws vector shapes (rectangle, ellipse, line, arrow, polygon, star) onto the image with an
/// optional fill. Used by the Shape tool. Fills respect an opacity so they can be blended over
/// the image; strokes are always opaque. For lines and arrows the rectangle acts as the segment
/// start→end span.
/// </summary>
public static class ShapeService
{
    /// <summary>
    /// Draws a shape described by <paramref name="rect"/> (pixel coordinates) onto a clone of
    /// <paramref name="bgr"/> and returns it. <paramref name="segments"/> is the number of sides
    /// (polygon) or points (star) and <paramref name="starRatio"/> the inner/outer radius ratio
    /// for stars; both are ignored for the other kinds. <paramref name="rotation"/> (degrees)
    /// rotates any closed shape (rectangle, ellipse, polygon, star) freely about its center;
    /// lines/arrows ignore it.
    /// </summary>
    public static Mat Apply(Mat bgr, ShapeKind kind, Rect rect, Vec3b strokeColor, int strokeWidth,
        bool fillEnabled, Vec3b fillColor, double fillOpacity,
        int segments = 5, double starRatio = 0.45, double rotation = 0)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        int w = bgr.Width;
        int h = bgr.Height;

        Rect clamped = GeometryHelper.ClampToSize(new Size(w, h), rect);
        if (clamped.Width < 1 || clamped.Height < 1)
        {
            return bgr.Clone();
        }

        Mat canvas = bgr.Clone();
        try
        {
            bool canFill = fillEnabled && fillOpacity > EditingGuard.Epsilon && IsFillable(kind);

            if (canFill)
            {
                var fillOverlay = new Mat(canvas.Size(), MatType.CV_8UC3, Scalar.All(0));
                try
                {
                    DrawOutline(fillOverlay, kind, clamped, fillColor, -1, segments, starRatio, rotation);
                    using var mask = new Mat(canvas.Size(), MatType.CV_32FC1, new Scalar((float)Math.Clamp(fillOpacity, 0.0, 1.0)));
                    Mat blended = canvas.BlendByMask(fillOverlay, mask);
                    canvas.Dispose();
                    canvas = blended;
                }
                finally
                {
                    fillOverlay.Dispose();
                }
            }

            if (strokeWidth > 0)
            {
                DrawOutline(canvas, kind, clamped, strokeColor, Math.Max(1, strokeWidth), segments, starRatio, rotation);
            }

            return canvas;
        }
        catch
        {
            canvas.Dispose();
            throw;
        }
    }

    private static bool IsFillable(ShapeKind kind)
        => kind is ShapeKind.Rectangle or ShapeKind.Ellipse or ShapeKind.Polygon or ShapeKind.Star;

    private static void DrawOutline(Mat target, ShapeKind kind, Rect rect, Vec3b color, int thickness,
        int segments, double starRatio, double rotation)
    {
        var start = new Point(rect.X, rect.Y);
        var end = new Point(rect.X + rect.Width, rect.Y + rect.Height);
        var scalar = new Scalar(color[0], color[1], color[2]);

        switch (kind)
        {
            case ShapeKind.Rectangle:
                if (IsAxisAligned(rotation))
                {
                    Cv2.Rectangle(target, start, end, scalar, thickness);
                }
                else
                {
                    DrawPointShape(target, kind, rect, scalar, thickness, segments, starRatio, rotation);
                }
                break;
            case ShapeKind.Ellipse:
                if (IsAxisAligned(rotation))
                {
                    var axisCenter = new Point((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
                    var axes = new Size(Math.Max(1, rect.Width / 2.0), Math.Max(1, rect.Height / 2.0));
                    Cv2.Ellipse(target, axisCenter, axes, 0, 0, 360, scalar, thickness);
                }
                else
                {
                    DrawPointShape(target, kind, rect, scalar, thickness, segments, starRatio, rotation);
                }
                break;
            case ShapeKind.Line:
                Cv2.Line(target, start, end, scalar, thickness);
                break;
            case ShapeKind.Arrow:
                Cv2.ArrowedLine(target, start, end, scalar, thickness);
                break;
            case ShapeKind.Polygon:
            case ShapeKind.Star:
                DrawPointShape(target, kind, rect, scalar, thickness, segments, starRatio, rotation);
                break;
        }
    }

    private static void DrawPointShape(Mat target, ShapeKind kind, Rect rect, Scalar scalar, int thickness,
        int segments, double starRatio, double rotation)
    {
        Point[] points = BuildShapePoints(kind, rect, segments, starRatio, rotation);

        if (thickness < 0)
        {
            // Fill: works for concave polygons such as a star.
            Cv2.FillPoly(target, new[] { points }, scalar);
            return;
        }

        // Stroke: draw each edge (including the closing edge) as a line.
        for (int i = 0; i < points.Length; i++)
        {
            Cv2.Line(target, points[i], points[(i + 1) % points.Length], scalar, thickness, LineTypes.AntiAlias);
        }
    }

    private static Point[] BuildShapePoints(ShapeKind kind, Rect rect, int segments, double starRatio, double rotation)
        => kind switch
        {
            ShapeKind.Rectangle => BuildRotatedRectPoints(rect, rotation),
            ShapeKind.Ellipse => BuildEllipsePoints(rect, rotation),
            ShapeKind.Star => BuildStarPoints(rect, Math.Max(3, segments), Math.Clamp(starRatio, 0.05, 0.95), rotation),
            _ => BuildPolygonPoints(rect, Math.Max(3, segments), rotation)
        };

    private static bool IsAxisAligned(double rotation)
        => Math.Abs(Math.Sin(rotation * Math.PI / 180.0)) < 1e-4;

    private static Point[] BuildRotatedRectPoints(Rect rect, double rotation)
    {
        double cx = rect.X + rect.Width / 2.0;
        double cy = rect.Y + rect.Height / 2.0;
        double a = rotation * Math.PI / 180.0;
        double ca = Math.Cos(a);
        double sa = Math.Sin(a);
        double hw = rect.Width / 2.0;
        double hh = rect.Height / 2.0;

        return new[]
        {
            Rotate(cx, cy, -hw, -hh, ca, sa),
            Rotate(cx, cy, hw, -hh, ca, sa),
            Rotate(cx, cy, hw, hh, ca, sa),
            Rotate(cx, cy, -hw, hh, ca, sa)
        };
    }

    private static Point[] BuildEllipsePoints(Rect rect, double rotation)
    {
        const int samples = 64;
        double cx = rect.X + rect.Width / 2.0;
        double cy = rect.Y + rect.Height / 2.0;
        double a = rotation * Math.PI / 180.0;
        double ca = Math.Cos(a);
        double sa = Math.Sin(a);
        double hw = rect.Width / 2.0;
        double hh = rect.Height / 2.0;

        var points = new Point[samples];
        for (int i = 0; i < samples; i++)
        {
            double t = 2.0 * Math.PI * i / samples;
            points[i] = Rotate(cx, cy, hw * Math.Cos(t), hh * Math.Sin(t), ca, sa);
        }
        return points;
    }

    private static Point Rotate(double cx, double cy, double ox, double oy, double ca, double sa)
        => new(cx + ox * ca - oy * sa, cy + ox * sa + oy * ca);

    private static Point[] BuildPolygonPoints(Rect rect, int sides, double rotation)
    {
        double cx = rect.X + rect.Width / 2.0;
        double cy = rect.Y + rect.Height / 2.0;
        double radius = Math.Min(rect.Width, rect.Height) / 2.0;
        double start = (rotation - 90) * Math.PI / 180.0;
        double step = 2.0 * Math.PI / sides;

        var points = new Point[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = start + i * step;
            points[i] = new Point(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a));
        }
        return points;
    }

    private static Point[] BuildStarPoints(Rect rect, int points, double ratio, double rotation)
    {
        double cx = rect.X + rect.Width / 2.0;
        double cy = rect.Y + rect.Height / 2.0;
        double outer = Math.Min(rect.Width, rect.Height) / 2.0;
        double inner = outer * ratio;
        double start = (rotation - 90) * Math.PI / 180.0;
        double step = Math.PI / points;

        var vertices = new Point[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            double r = (i % 2 == 0) ? outer : inner;
            double a = start + i * step;
            vertices[i] = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        return vertices;
    }
}
