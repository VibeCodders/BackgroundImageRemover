using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

public interface IImageLoaderService
{
    Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// Decodes an image file into a BGR Mat (plus the alpha channel when the file has one,
/// so reopening a previously exported cutout keeps its transparency instead of being
/// treated as a fresh opaque photo). Note: EXIF orientation is not auto-applied
/// (known v1 limitation for phone-camera photos).
/// </summary>
public sealed class ImageLoaderService : IImageLoaderService
{
    public Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            // Peek at the raw channel count so a transparent PNG (4 channels) can be
            // decoded with its alpha intact.
            using (var probe = Cv2.ImRead(filePath, ImreadModes.Unchanged))
            {
                if (probe.Empty())
                {
                    throw new InvalidOperationException($"Could not decode image: {filePath}");
                }
                if (probe.Channels() != 4)
                {
                    return LoadWithoutAlpha(filePath);
                }
            }

            var mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                mat.Dispose();
                throw new InvalidOperationException($"Could not decode image: {filePath}");
            }

            var channels = Cv2.Split(mat);
            Mat bgr = new();
            Mat alpha = channels[3].Clone();
            try
            {
                Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
            }
            catch
            {
                bgr.Dispose();
                throw;
            }
            finally
            {
                foreach (var c in channels) c.Dispose();
                mat.Dispose();
            }

            return new LoadedImage(filePath, bgr, alpha);
        }, ct);
    }

    private static LoadedImage LoadWithoutAlpha(string filePath)
    {
        var mat = Cv2.ImRead(filePath, ImreadModes.Color);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException($"Could not decode image: {filePath}");
        }

        return new LoadedImage(filePath, mat);
    }
}
