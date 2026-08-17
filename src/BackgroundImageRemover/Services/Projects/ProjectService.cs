using System.IO;
using System.Text.Json;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Projects;

public interface IProjectService
{
    Task SaveAsync(
        string path,
        Mat originalBgr,
        Mat? originalAlpha,
        Mat? workingBgr,
        Mat? workingAlpha,
        ProjectDocument settings,
        CancellationToken ct = default);

    Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Decoded contents of a <c>.ibrproj</c> file. Ownership of the Mats transfers to the caller,
/// which is responsible for disposing them (see <see cref="Dispose"/>).
/// </summary>
public sealed class LoadedProject : IDisposable
{
    public required ProjectDocument Settings { get; init; }
    public required Mat OriginalBgr { get; init; }
    public Mat? OriginalAlpha { get; init; }
    public Mat? WorkingBgr { get; init; }
    public Mat? WorkingAlpha { get; init; }

    public void Dispose()
    {
        OriginalBgr.Dispose();
        OriginalAlpha?.Dispose();
        WorkingBgr?.Dispose();
        WorkingAlpha?.Dispose();
    }
}

/// <summary>
/// Persists a full editing session as a self-contained <c>.ibrproj</c> file: the original
/// image and the working cutout (BGR + alpha) are embedded as PNG (base64) so the project can
/// be moved around and reopened without the source files. The alpha channels are preserved so
/// a previously cleaned cutout round-trips exactly.
/// </summary>
public sealed class ProjectService : IProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public Task SaveAsync(
        string path,
        Mat originalBgr,
        Mat? originalAlpha,
        Mat? workingBgr,
        Mat? workingAlpha,
        ProjectDocument settings,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var file = new ProjectFile
            {
                Version = settings.Version,
                Settings = settings,
                OriginalImagePng = EncodeBgrWithAlphaBase64(originalBgr, originalAlpha),
                WorkingImagePng = workingBgr is null || workingAlpha is null
                    ? null
                    : EncodeBgrWithAlphaBase64(workingBgr, workingAlpha)
            };

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(path, json);
        }, ct);
    }

    public Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ProjectFile>(json)
                ?? throw new InvalidOperationException("The project file is empty or malformed.");

            if (file.Version > ProjectDocument.FormatVersion)
            {
                throw new InvalidOperationException(
                    $"This project was saved by a newer version of the app (format {file.Version}); update the app to open it.");
            }

            if (file.OriginalImagePng is null)
            {
                throw new InvalidOperationException("The project file has no embedded image.");
            }

            var (originalBgr, originalAlpha) = DecodeBgrWithAlphaBase64(file.OriginalImagePng);
            Mat? workingBgr = null;
            Mat? workingAlpha = null;

            try
            {
                if (file.WorkingImagePng is not null)
                {
                    (workingBgr, workingAlpha) = DecodeBgrWithAlphaBase64(file.WorkingImagePng);
                }

                return new LoadedProject
                {
                    Settings = file.Settings ?? new ProjectDocument(),
                    OriginalBgr = originalBgr,
                    OriginalAlpha = originalAlpha,
                    WorkingBgr = workingBgr,
                    WorkingAlpha = workingAlpha
                };
            }
            catch
            {
                originalBgr.Dispose();
                originalAlpha?.Dispose();
                workingBgr?.Dispose();
                workingAlpha?.Dispose();
                throw;
            }
        }, ct);
    }

    private static string EncodeBgrWithAlphaBase64(Mat bgr, Mat? alpha)
    {
        if (alpha is null)
        {
            return EncodePngBase64(bgr);
        }

        using var bgra = new Mat();
        Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        ReplaceAlphaChannel(bgra, alpha);
        return EncodePngBase64(bgra);
    }

    private static void ReplaceAlphaChannel(Mat bgra, Mat newAlpha)
    {
        var channels = Cv2.Split(bgra);
        try
        {
            newAlpha.CopyTo(channels[3]);
            Cv2.Merge(channels, bgra);
        }
        finally
        {
            foreach (var c in channels) c.Dispose();
        }
    }

    private static string EncodePngBase64(Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var buffer);
        return Convert.ToBase64String(buffer);
    }

    private static (Mat Bgr, Mat? Alpha) DecodeBgrWithAlphaBase64(string base64)
    {
        var mat = DecodePngBase64(base64);
        if (mat.Channels() != 4)
        {
            return (mat, null);
        }

        var bgr = new Mat();
        Mat? alpha = null;
        Mat[]? channels = null;
        try
        {
            channels = Cv2.Split(mat);
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
            alpha = channels[3].Clone();
            return (bgr, alpha);
        }
        catch
        {
            bgr.Dispose();
            alpha?.Dispose();
            throw;
        }
        finally
        {
            if (channels is not null)
            {
                foreach (var c in channels) c.Dispose();
            }
            mat.Dispose();
        }
    }

    private static Mat DecodePngBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var mat = Cv2.ImDecode(bytes, ImreadModes.Unchanged);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException("Could not decode the embedded project image.");
        }
        return mat;
    }

    /// <summary>On-disk JSON envelope: settings plus the embedded image payloads.</summary>
    private sealed class ProjectFile
    {
        public int Version { get; set; } = 1;
        public ProjectDocument? Settings { get; set; }
        public string? OriginalImagePng { get; set; }
        public string? WorkingImagePng { get; set; }
    }
}
