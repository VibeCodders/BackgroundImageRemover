using System.IO;

namespace BackgroundImageRemover.Services.Logging;

/// <summary>Log severity level.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public interface IFileLogService
{
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// Simple rolling-by-day file logger under %LOCALAPPDATA%\BackgroundImageRemover\logs\, with no
/// external dependency. Writes are best-effort: a logging failure must never crash the app.
/// </summary>
public sealed class FileLogService : IFileLogService
{
    private readonly object _lock = new();
    private readonly string _logDirectory;

    /// <summary>Log files older than this are deleted on startup to keep the folder bounded.</summary>
    private static readonly TimeSpan MaxLogAge = TimeSpan.FromDays(30);

    public FileLogService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BackgroundImageRemover", "logs"))
    {
    }

    /// <summary>Internal for tests: logs into the given directory instead of %LOCALAPPDATA%.</summary>
    internal FileLogService(string logDirectory)
    {
        _logDirectory = logDirectory;
        PruneOldLogs();
    }

    private string CurrentLogFile => Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warning(string message) => Write(LogLevel.Warning, message);
    public void Error(string message, Exception? exception = null)
        => Write(LogLevel.Error, exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(LogLevel level, string message)
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

    /// <summary>Deletes log files older than <see cref="MaxLogAge"/> (best-effort, once per process).</summary>
    private void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now - MaxLogAge;
            foreach (var file in Directory.EnumerateFiles(_logDirectory, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Best-effort cleanup; logging must never crash startup.
        }
    }
}
