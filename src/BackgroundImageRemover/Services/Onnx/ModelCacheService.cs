using System.IO;
using System.Net.Http;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Services.Onnx;

public interface IModelCacheService
{
    string CachedModelPath(OnnxModelKind kind);
    bool IsModelCached(OnnxModelKind kind);
    Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct);

    /// <summary>Generic named-file download/cache, used for models that aren't part of <see cref="OnnxModelKind"/> (e.g. SAM's encoder/decoder pair).</summary>
    Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<ModelDownloadProgress>? progress, CancellationToken ct);
    bool IsNamedFileCached(string fileName);
}

/// <summary>
/// Downloads and caches ONNX models on first use under %LOCALAPPDATA%\BackgroundImageRemover\models\.
/// </summary>
public sealed class ModelCacheService : IModelCacheService
{
    private const long MinimumValidFileSizeBytes = 1_000_000; // sanity check against HTML error pages

    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BackgroundImageRemover", "models");

    public ModelCacheService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string CachedModelPath(OnnxModelKind kind) => Path.Combine(_modelsDirectory, OnnxModelCatalog.Get(kind).FileName);

    public bool IsModelCached(OnnxModelKind kind) => IsNamedFileCached(OnnxModelCatalog.Get(kind).FileName);

    public bool IsNamedFileCached(string fileName)
    {
        string path = Path.Combine(_modelsDirectory, fileName);
        return File.Exists(path) && new FileInfo(path).Length >= MinimumValidFileSizeBytes;
    }

    public Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        var definition = OnnxModelCatalog.Get(kind);
        return EnsureNamedFileAvailableAsync(definition.FileName, definition.DownloadUrl, progress, ct);
    }

    public async Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        string finalPath = Path.Combine(_modelsDirectory, fileName);
        if (IsNamedFileCached(fileName))
        {
            return finalPath;
        }

        Directory.CreateDirectory(_modelsDirectory);
        string tmpPath = finalPath + ".tmp";

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
                    $"Downloaded file '{fileName}' looks invalid (only {downloadedSize} bytes). The download URL may have changed.");
            }

            File.Move(tmpPath, finalPath, overwrite: true);
            return finalPath;
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
