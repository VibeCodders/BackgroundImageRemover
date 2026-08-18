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
}
