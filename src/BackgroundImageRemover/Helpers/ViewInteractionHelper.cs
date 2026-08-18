using System.Diagnostics;
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
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".webp", ".gif", ".tif", ".tiff", ".ico"
    };

    /// <summary>Extension of the app's self-contained project files.</summary>
    private const string ProjectExtension = ".ibrproj";

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
    /// Checks whether the specified file path is something the app can open in a tab:
    /// a supported image or a <c>.ibrproj</c> project file.
    /// </summary>
    public static bool IsSupportedFile(string path)
        => IsSupportedImage(path) || IsProjectFile(path);

    /// <summary>Checks whether the specified file path is a <c>.ibrproj</c> project file.</summary>
    public static bool IsProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        return string.Equals(Path.GetExtension(path), ProjectExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Handles DragOver for image/project file drop targets.
    /// </summary>
    public static void HandleImageDragOver(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 } && IsSupportedFile(files[0]))
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

    /// <summary>
    /// Opens Explorer at the given file (selecting it) or at its containing folder, without
    /// throwing: a failed launch is reported by returning false.
    /// </summary>
    public static bool RevealInExplorer(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string argument = File.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{Path.GetDirectoryName(path)}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks clipboard for an image (BitmapSource or image file) and returns it, or null if none present.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource? TryGetClipboardImage()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                return Clipboard.GetImage();
            }

            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0 && files[0] is { } filePath && IsSupportedImage(filePath))
                {
                    return new System.Windows.Media.Imaging.BitmapImage(new Uri(filePath));
                }
            }
        }
        catch
        {
            // Clipboard access can throw if locked by another process
        }
        return null;
    }
}
