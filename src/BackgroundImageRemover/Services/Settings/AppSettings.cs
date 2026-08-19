namespace BackgroundImageRemover.Services.Settings;

/// <summary>UI color scheme. System follows the Windows light/dark preference.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>UI language. System uses the OS display language (falling back to English).</summary>
public enum AppLanguage
{
    System,
    English,
    Italian
}

public sealed class AppSettings
{
    public bool UseGpuForOnnx { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.System;

    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>When true, the last opened files/projects are reopened on the next launch.</summary>
    public bool ReopenLastSession { get; set; }

    /// <summary>Periodically saves dirty documents to a recovery folder so work survives a crash.</summary>
    public bool EnableAutosave { get; set; } = true;

    /// <summary>Minutes between autosave passes (clamped to at least 1).</summary>
    public int AutosaveIntervalMinutes { get; set; } = 2;

    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Projects saved with "Save", listed so a half-finished job can be reopened and continued.</summary>
    public List<string> RecentProjects { get; set; } = new();

    // Last main-window geometry, restored on the next launch.
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>Last folder chosen for batch input, used as the starting point next time.</summary>
    public string? LastBatchInputFolder { get; set; }

    /// <summary>Last folder chosen for batch output, offered as the default next time.</summary>
    public string? LastBatchOutputFolder { get; set; }

    /// <summary>Last batch output format chosen (a <c>BatchOutputKind</c> enum name), restored next time.</summary>
    public string? LastBatchOutputKind { get; set; }

    /// <summary>Last batch JPEG quality, restored next time.</summary>
    public int LastBatchJpegQuality { get; set; } = 95;

    /// <summary>Last "skip existing outputs" choice for the batch dialog.</summary>
    public bool LastBatchSkipExisting { get; set; }

    /// <summary>Last background image chosen for JPEG batch output, restored next time.</summary>
    public string? LastBatchBackgroundImagePath { get; set; }

    // --- Last single-image export settings, restored when a new document opens ---

    /// <summary>Last export background mode (an <c>ExportBackgroundMode</c> enum name).</summary>
    public string? LastExportBackgroundMode { get; set; }

    /// <summary>Last solid export background color (WPF color string).</summary>
    public string? LastExportSolidColor { get; set; }

    /// <summary>Last gradient export top color (WPF color string).</summary>
    public string? LastExportGradientTopColor { get; set; }

    /// <summary>Last gradient export bottom color (WPF color string).</summary>
    public string? LastExportGradientBottomColor { get; set; }

    /// <summary>Last blur-background export radius.</summary>
    public double LastExportBlurRadius { get; set; } = 10;

    /// <summary>Last background-image export path.</summary>
    public string? LastExportBackgroundImagePath { get; set; }

    /// <summary>Last JPEG/WebP export quality, 1..100.</summary>
    public int LastExportJpegQuality { get; set; } = 95;

    /// <summary>Last drop-shadow export toggle.</summary>
    public bool LastExportDropShadowEnabled { get; set; }

    /// <summary>Last drop-shadow offset (pixels).</summary>
    public double LastExportShadowOffset { get; set; } = 12;

    /// <summary>Last drop-shadow blur (pixels).</summary>
    public double LastExportShadowBlur { get; set; } = 6;

    /// <summary>Last drop-shadow opacity, 0..1.</summary>
    public double LastExportShadowOpacity { get; set; } = 0.45;

    /// <summary>Last drop-shadow color (WPF color string).</summary>
    public string? LastExportShadowColor { get; set; }
}
