namespace BackgroundImageRemover.Services.Settings;

public sealed class AppSettings
{
    public bool UseGpuForOnnx { get; set; }
    public List<string> RecentFiles { get; set; } = new();
}
