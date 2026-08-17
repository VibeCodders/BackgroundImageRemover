namespace BackgroundImageRemover.Services.Settings;

public sealed class AppSettings
{
    public bool UseGpuForOnnx { get; set; }
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Projects saved with "Save", listed so a half-finished job can be reopened and continued.</summary>
    public List<string> RecentProjects { get; set; } = new();
}
