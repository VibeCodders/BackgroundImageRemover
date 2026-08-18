using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.Models;

/// <summary>
/// How batch-processed cutouts are written to disk. PNG keeps the transparency; JPEG cannot
/// store alpha, so the cutout is composited onto a background first.
/// </summary>
public sealed class BatchExportOptions
{
    /// <summary>True to write JPEG files; false writes transparent PNG cutouts.</summary>
    public bool ExportJpeg { get; set; }

    /// <summary>When true, files whose output already exists are left untouched (no re-export).</summary>
    public bool SkipExisting { get; set; }

    /// <summary>JPEG quality, 1..100.</summary>
    public int JpegQuality { get; set; } = 95;

    /// <summary>Background the cutout is composited onto for JPEG output.</summary>
    public ExportBackgroundMode BackgroundMode { get; set; } = ExportBackgroundMode.SolidColor;

    public WpfColor SolidColor { get; set; } = WpfColor.FromRgb(255, 255, 255);
    public WpfColor GradientTop { get; set; } = WpfColor.FromRgb(255, 255, 255);
    public WpfColor GradientBottom { get; set; } = WpfColor.FromRgb(120, 120, 120);
    public double BlurRadius { get; set; } = 10;
}
