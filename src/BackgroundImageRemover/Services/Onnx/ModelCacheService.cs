using System.IO;
using System.Net.Http;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Services.Onnx;

public interface IModelCacheService
{
    string CachedModelPath(OnnxModelKind kind);
    bool IsModelCached(OnnxModelKind kind);
    Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct);
}

/// <summary>
/// Downloads and caches ONNX models on first use, one file per <see cref="OnnxModelKind"/>.
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

    public bool IsModelCached(OnnxModelKind kind)
    {
        string path = CachedModelPath(kind);
        return File.Exists(path) && new FileInfo(path).Length >= MinimumValidFileSizeBytes;
    }

    public async Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (IsModelCached(kind))
        {
            return CachedModelPath(kind);
        }

        var definition = OnnxModelCatalog.Get(kind);
        string finalPath = CachedModelPath(kind);
        Directory.CreateDirectory(_modelsDirectory);
        string tmpPath = finalPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(definition.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
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
