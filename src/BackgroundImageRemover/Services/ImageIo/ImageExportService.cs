using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

public interface IImageExportService
{
    Task ExportPngAsync(Mat bgra, string filePath, CancellationToken ct = default);
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
}
