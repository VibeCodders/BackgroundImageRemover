using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Paints circular brush strokes onto a binary mask. Consecutive points are connected with a
/// thick line, so fast mouse movement leaves a continuous stroke instead of isolated dots.
/// Used by the painting tools (Mosaic mask, Heal mask).
/// </summary>
public static class MaskBrushHelper
{
    /// <summary>
    /// Paints a stroke segment between <paramref name="from"/> and <paramref name="to"/> on the
    /// mask (in-place) with the given brush radius (pixels). A zero-length segment paints a dot.
    /// </summary>
    public static void StampSegment(Mat mask, WpfPoint from, WpfPoint to, double pixelRadius)
    {
        int r = Math.Max(1, (int)Math.Round(pixelRadius));
        var p1 = new Point((int)Math.Round(from.X), (int)Math.Round(from.Y));
        var p2 = new Point((int)Math.Round(to.X), (int)Math.Round(to.Y));

        // The thick line connects the two points; the circle rounds the end cap.
        Cv2.Line(mask, p1, p2, Scalar.All(255), r * 2);
        Cv2.Circle(mask, p2, r, Scalar.All(255), -1);
    }
}
