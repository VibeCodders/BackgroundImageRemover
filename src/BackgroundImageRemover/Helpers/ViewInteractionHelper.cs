using System.IO;
using System.Windows;
using System.Windows.Media;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Common utility functions for zoom/pan interaction, file validation, and UI helpers.
/// </summary>
public static class ViewInteractionHelper
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp"
    };

    /// <summary>
    /// Checks whether the specified file path has a supported image extension.
    /// </summary>
    public static bool IsSupportedImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        var ext = Path.GetExtension(path);
        return SupportedImageExtensions.Contains(ext);
    }

    /// <summary>
    /// Handles DragOver for image file drop targets.
    /// </summary>
    public static void HandleImageDragOver(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 } && IsSupportedImage(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Computes the new scale and translation for zoom centered at the cursor position.
    /// </summary>
    public static bool ComputeZoom(
        Point cursor,
        int wheelDelta,
        double currentScale,
        Point currentTranslate,
        double minScale,
        double maxScale,
        out double newScale,
        out Point newTranslate)
    {
        double factor = wheelDelta > 0 ? 1.1 : 1.0 / 1.1;
        newScale = Math.Clamp(currentScale * factor, minScale, maxScale);

        if (Math.Abs(newScale - currentScale) < 1e-6)
        {
            newTranslate = currentTranslate;
            return false;
        }

        double px = (cursor.X - currentTranslate.X) / currentScale;
        double py = (cursor.Y - currentTranslate.Y) / currentScale;

        newTranslate = new Point(cursor.X - px * newScale, cursor.Y - py * newScale);
        return true;
    }

    /// <summary>
    /// Applies pan translation delta to an existing translate point.
    /// </summary>
    public static Point ComputePan(Point panStartPoint, Point panStartTranslate, Point currentPoint)
    {
        return new Point(
            panStartTranslate.X + (currentPoint.X - panStartPoint.X),
            panStartTranslate.Y + (currentPoint.Y - panStartPoint.Y));
    }
}
