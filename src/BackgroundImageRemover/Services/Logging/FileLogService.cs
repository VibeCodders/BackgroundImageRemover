using System.IO;

namespace BackgroundImageRemover.Services.Logging;

public interface IFileLogService
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// Simple rolling-by-day file logger under %LOCALAPPDATA%\BackgroundImageRemover\logs\, with no
/// external dependency. Writes are best-effort: a logging failure must never crash the app.
/// </summary>
public sealed class FileLogService : IFileLogService
{
    private readonly object _lock = new();
    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BackgroundImageRemover", "logs");

    private string CurrentLogFile => Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(CurrentLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never throw and take down the app.
        }
    }
}
