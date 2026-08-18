using ExportBackgroundModeEnum = BackgroundImageRemover.Models.ExportBackgroundMode;
using BrushModeEnum = BackgroundImageRemover.Models.BrushMode;

namespace BackgroundImageRemover.Models;

/// <summary>
/// The editable settings of a saved project (everything except the embedded image data,
/// which the <see cref="Services.Projects.ProjectService"/> handles separately).
/// Enum values are stored as their names for forward/backward tolerance.
/// </summary>
public sealed class ProjectDocument
{
    /// <summary>Current on-disk format version written by this app.</summary>
    public const int FormatVersion = 1;

    public int Version { get; set; } = FormatVersion;

    /// <summary>Base display name (without the "(cutout)" suffix).</summary>
    public string? Title { get; set; }

    public string SelectedStrategy { get; set; } = nameof(StrategyKind.ChromaKey);

    public double ChromaKeyTolerance { get; set; } = 20;
    public bool ChromaKeySpillSuppression { get; set; } = true;
    public byte[]? ChromaKeyDetectedColorBgr { get; set; } // [B, G, R]

    public string OnnxModel { get; set; } = nameof(OnnxModelKind.U2NetP);
    public int OnnxFeatherPixels { get; set; } = 2;
    public bool OnnxEnableAlphaMatting { get; set; }

    public string ExportBackgroundMode { get; set; } = nameof(ExportBackgroundModeEnum.Transparent);
    public string ExportSolidColor { get; set; } = "#FFFFFFFF";
    public string? ExportBackgroundImagePath { get; set; }
    public double ExportBlurRadius { get; set; } = 10;
    public string ExportGradientTopColor { get; set; } = "#FFFFFFFF";
    public string ExportGradientBottomColor { get; set; } = "#FF787878";
    public bool ExportDropShadowEnabled { get; set; }
    public int ExportJpegQuality { get; set; } = 95;
    public double ExportShadowOffset { get; set; } = 12;
    public double ExportShadowBlur { get; set; } = 6;
    public double ExportShadowOpacity { get; set; } = 0.45;

    public double BrushRadius { get; set; } = 20;
    public double BrushHardness { get; set; } = 0.5;
    public string BrushMode { get; set; } = nameof(BrushModeEnum.Restore);
    public double MagicWandTolerance { get; set; } = 20;

    /// <summary>GrabCut subject rectangle in preview coordinates; null when none was drawn. [X, Y, Width, Height]</summary>
    public int[]? GrabCutRect { get; set; }

    /// <summary>SAM prompt point in preview coordinates; null when none was clicked. [X, Y]</summary>
    public int[]? SamPoint { get; set; }
}
