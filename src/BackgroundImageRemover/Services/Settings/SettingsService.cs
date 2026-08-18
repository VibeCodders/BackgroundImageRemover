using System.IO;
using System.Text.Json;

namespace BackgroundImageRemover.Services.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void AddRecentFile(string path);
    void AddRecentProject(string path);
    void ClearRecentFiles();
    void ClearRecentProjects();
}

/// <summary>Loads/saves a small JSON settings file in %LOCALAPPDATA%\BackgroundImageRemover\settings.json.</summary>
public sealed class SettingsService : ISettingsService
{
    private const int MaxRecentFiles = 10;
    private const int MaxRecentProjects = 10;
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BackgroundImageRemover", "settings.json");

    public AppSettings Current { get; }

    public SettingsService()
    {
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt/unreadable settings file: fall back to defaults rather than failing startup.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort persistence; not critical to app function.
        }
    }

    public void AddRecentFile(string path)
    {
        Current.RecentFiles.Remove(path);
        Current.RecentFiles.Insert(0, path);
        while (Current.RecentFiles.Count > MaxRecentFiles)
        {
            Current.RecentFiles.RemoveAt(Current.RecentFiles.Count - 1);
        }
        Save();
    }

    public void AddRecentProject(string path)
    {
        Current.RecentProjects.Remove(path);
        Current.RecentProjects.Insert(0, path);
        while (Current.RecentProjects.Count > MaxRecentProjects)
        {
            Current.RecentProjects.RemoveAt(Current.RecentProjects.Count - 1);
        }
        Save();
    }

    public void ClearRecentFiles()
    {
        Current.RecentFiles.Clear();
        Save();
    }

    public void ClearRecentProjects()
    {
        Current.RecentProjects.Clear();
        Save();
    }
}
