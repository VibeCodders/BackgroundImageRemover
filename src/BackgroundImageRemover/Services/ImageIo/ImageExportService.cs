using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

public interface IImageExportService
{
    Task ExportPngAsync(Mat bgra, string filePath, CancellationToken ct = default);

    /// <summary>Writes a JPEG (BGR input; the caller composites any transparency first).</summary>
    Task ExportJpgAsync(Mat bgr, string filePath, int quality = 95, CancellationToken ct = default);

    /// <summary>Writes a WebP image (BGRA input; transparency is preserved).</summary>
    Task ExportWebpAsync(Mat bgra, string filePath, int quality = 90, CancellationToken ct = default);
}

public sealed class ImageExportService : IImageExportService
{
    public Task ExportPngAsync(Mat bgra, string filePath, CancellationToken ct = default)
    {
        var clone = bgra.Clone();
        return Task.Run(() =>
        {
            try
            {
                if (!Cv2.ImWrite(filePath, clone))
                {
                    throw new InvalidOperationException($"Could not write PNG file: {filePath}");
                }
            }
            finally
            {
                clone.Dispose();
            }
        }, ct);
    }

    public Task ExportJpgAsync(Mat bgr, string filePath, int quality = 95, CancellationToken ct = default)
    {
        var clone = bgr.Clone();
        return Task.Run(() =>
        {
            try
            {
                if (!Cv2.ImWrite(filePath, clone,
                        new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, Math.Clamp(quality, 1, 100)) }))
                {
                    throw new InvalidOperationException($"Could not write JPEG file: {filePath}");
                }
            }
            finally
            {
                clone.Dispose();
            }
        }, ct);
    }

    public Task ExportWebpAsync(Mat bgra, string filePath, int quality = 90, CancellationToken ct = default)
    {
        var clone = bgra.Clone();
        return Task.Run(() =>
        {
            try
            {
                // OpenCvSharp 4.10 has no named flag for WebP quality; 64 is IMWRITE_WEBP_QUALITY.
                if (!Cv2.ImWrite(filePath, clone,
                        new[] { new ImageEncodingParam((ImwriteFlags)64, Math.Clamp(quality, 1, 100)) }))
                {
                    throw new InvalidOperationException($"Could not write WebP file: {filePath}");
                }
            }
            finally
            {
                clone.Dispose();
            }
        }, ct);
    }
}
