using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>A single freehand pen stroke with its (mutable, accumulating) point list and radius
/// (in source-image pixels).</summary>
public sealed record PenStroke(List<Point> Points, int RadiusPx);

/// <summary>
/// Renders freehand pen strokes onto the image as thick polylines with rounded tips. Used by
/// the Pen tool; separated into its own service so the geometry is unit-testable.
/// </summary>
public static class PenRenderService
{
    /// <summary>
    /// Draws <paramref name="strokes"/> in <paramref name="color"/> onto a clone of
    /// <paramref name="bgr"/> and returns it. Each point is capped with a filled circle so the
    /// stroke has rounded, marker-like ends even for a single click.
    /// </summary>
    public static Mat Draw(Mat bgr, IReadOnlyList<PenStroke>? strokes, Vec3b color)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        var result = bgr.Clone();
        if (strokes is null || strokes.Count == 0)
        {
            return result;
        }

        var scalar = new Scalar(color[0], color[1], color[2]);
        try
        {
            foreach (var stroke in strokes)
            {
                DrawStroke(result, stroke, scalar);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static void DrawStroke(Mat target, PenStroke stroke, Scalar color)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        int radius = Math.Max(1, stroke.RadiusPx);
        int thickness = radius * 2;

        for (int i = 1; i < stroke.Points.Count; i++)
        {
            Cv2.Line(target, stroke.Points[i - 1], stroke.Points[i], color, thickness, LineTypes.AntiAlias);
        }

        foreach (var point in stroke.Points)
        {
            Cv2.Circle(target, point, radius, color, -1, LineTypes.AntiAlias);
        }
    }
}
