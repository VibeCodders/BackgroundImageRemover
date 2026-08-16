using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

public interface IImageLoaderService
{
    Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// Decodes an image file into a BGR Mat. Note: EXIF orientation is not auto-applied
/// (known v1 limitation for phone-camera photos).
/// </summary>
public sealed class ImageLoaderService : IImageLoaderService
{
    public Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var mat = Cv2.ImRead(filePath, ImreadModes.Color);
            if (mat.Empty())
            {
                mat.Dispose();
                throw new InvalidOperationException($"Could not decode image: {filePath}");
            }

            return new LoadedImage(filePath, mat);
        }, ct);
    }
}
