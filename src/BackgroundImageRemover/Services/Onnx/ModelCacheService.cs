using System.IO;
using System.Net.Http;

namespace BackgroundImageRemover.Services.Onnx;

public interface IModelCacheService
{
    string CachedModelPath { get; }
    bool IsModelCached { get; }
    Task<string> EnsureModelAvailableAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct);
}

/// <summary>
/// Downloads and caches the U2Netp ONNX model on first use. The source URL is a public
/// release asset and can be overridden via the BIR_U2NETP_URL environment variable if it
/// ever moves.
/// </summary>
public sealed class ModelCacheService : IModelCacheService
{
    private const string DefaultModelUrl = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx";
    private const long MinimumValidFileSizeBytes = 1_000_000; // sanity check against HTML error pages

    private readonly HttpClient _httpClient;

    public ModelCacheService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string CachedModelPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BackgroundImageRemover", "models", "u2netp.onnx");

    public bool IsModelCached => File.Exists(CachedModelPath) && new FileInfo(CachedModelPath).Length >= MinimumValidFileSizeBytes;

    public async Task<string> EnsureModelAvailableAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (IsModelCached)
        {
            return CachedModelPath;
        }

        string url = Environment.GetEnvironmentVariable("BIR_U2NETP_URL") is { Length: > 0 } overrideUrl
            ? overrideUrl
            : DefaultModelUrl;

        var directory = Path.GetDirectoryName(CachedModelPath)!;
        Directory.CreateDirectory(directory);
        string tmpPath = CachedModelPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            await using (var httpStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    progress?.Report(new ModelDownloadProgress(totalRead, totalBytes));
                }
            }

            var downloadedSize = new FileInfo(tmpPath).Length;
            if (downloadedSize < MinimumValidFileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Downloaded model file looks invalid (only {downloadedSize} bytes). The model URL may have changed.");
            }

            File.Move(tmpPath, CachedModelPath, overwrite: true);
            return CachedModelPath;
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
            throw;
        }
    }
}
