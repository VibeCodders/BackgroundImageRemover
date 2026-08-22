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
    Arrow
}

/// <summary>
/// Draws vector shapes (rectangle, ellipse, line, arrow) onto the image with an optional fill.
/// Used by the Shape tool. Fills respect an opacity so they can be blended over the image;
/// strokes are always opaque.
/// </summary>
public static class ShapeService
{
    /// <summary>
    /// Draws a shape described by <paramref name="rect"/> (pixel coordinates) onto a clone of
    /// <paramref name="bgr"/> and returns it. For rectangles and ellipses a semi-transparent fill
    /// is applied when <paramref name="fillEnabled"/> is true; for lines and arrows the rectangle
    /// acts as the segment start→end span.
    /// </summary>
    public static Mat Apply(Mat bgr, ShapeKind kind, Rect rect, Vec3b strokeColor, int strokeWidth, bool fillEnabled, Vec3b fillColor, double fillOpacity)
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
            bool canFill = fillEnabled && fillOpacity > EditingGuard.Epsilon
                && (kind == ShapeKind.Rectangle || kind == ShapeKind.Ellipse);

            if (canFill)
            {
                var fillOverlay = new Mat(canvas.Size(), MatType.CV_8UC3, Scalar.All(0));
                try
                {
                    DrawOutline(fillOverlay, kind, clamped, fillColor, -1);
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
                DrawOutline(canvas, kind, clamped, strokeColor, Math.Max(1, strokeWidth));
            }

            return canvas;
        }
        catch
        {
            canvas.Dispose();
            throw;
        }
    }

    private static void DrawOutline(Mat target, ShapeKind kind, Rect rect, Vec3b color, int thickness)
    {
        var start = new Point(rect.X, rect.Y);
        var end = new Point(rect.X + rect.Width, rect.Y + rect.Height);
        var scalar = new Scalar(color[0], color[1], color[2]);

        switch (kind)
        {
            case ShapeKind.Rectangle:
                Cv2.Rectangle(target, start, end, scalar, thickness);
                break;
            case ShapeKind.Ellipse:
                var center = new Point((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
                var axes = new Size(Math.Max(1, rect.Width / 2.0), Math.Max(1, rect.Height / 2.0));
                Cv2.Ellipse(target, center, axes, 0, 0, 360, scalar, thickness);
                break;
            case ShapeKind.Line:
                Cv2.Line(target, start, end, scalar, thickness);
                break;
            case ShapeKind.Arrow:
                Cv2.ArrowedLine(target, start, end, scalar, thickness);
                break;
        }
    }
}
