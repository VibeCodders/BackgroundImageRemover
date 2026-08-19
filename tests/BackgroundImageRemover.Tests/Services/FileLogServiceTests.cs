using System.IO;
using BackgroundImageRemover.Services.Logging;

namespace BackgroundImageRemover.Tests.Services;

public class FileLogServiceTests
{
    [Fact]
    public void Constructor_PrunesLogsOlderThanThirtyDays()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"logs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string oldFile = Path.Combine(dir, "2020-01-01.log");
            string recentFile = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            File.WriteAllText(oldFile, "old");
            File.WriteAllText(recentFile, "recent");
            File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-40));

            _ = new FileLogService(dir);

            Assert.False(File.Exists(oldFile), "logs older than 30 days must be removed");
            Assert.True(File.Exists(recentFile), "recent logs must be kept");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Info_WritesToDailyLogFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"logs_{Guid.NewGuid():N}");
        try
        {
            var service = new FileLogService(dir);
            service.Info("hello");

            string logFile = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            Assert.True(File.Exists(logFile));
            Assert.Contains("hello", File.ReadAllText(logFile));
            Assert.Contains("[Info]", File.ReadAllText(logFile));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
