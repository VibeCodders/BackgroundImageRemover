using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

public interface IImageLoaderService
{
    Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default);
    Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
        => throw new NotImplementedException();
    Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
        => throw new NotImplementedException();
}

/// <summary>
/// Decodes an image file or stream into a BGR Mat (plus the alpha channel when the image has one,
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

            using var mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                throw new InvalidOperationException($"Could not decode image: {filePath}");
            }

            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(mat);
            return new LoadedImage(filePath, bgr, alpha);
        }, ct);
    }

    public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                throw new InvalidOperationException("Could not decode image bytes.");
            }

            if (mat.Channels() == 4)
            {
                var (bgr, alpha) = BackgroundCompositingService.SplitBgra(mat);
                return new LoadedImage(sourceName, bgr, alpha);
            }

            if (mat.Channels() == 3)
            {
                return new LoadedImage(sourceName, mat.Clone());
            }

            using var bgrMat = new Mat();
            Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR);
            return new LoadedImage(sourceName, bgrMat.Clone());
        }, ct);
    }

    public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
    {
        return Task.Run(() =>
        {
            var mat = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(bitmapSource);
            if (mat.Channels() == 4)
            {
                var (bgr, alpha) = BackgroundCompositingService.SplitBgra(mat);
                mat.Dispose();
                return new LoadedImage(sourceName, bgr, alpha);
            }

            if (mat.Channels() == 3)
            {
                return new LoadedImage(sourceName, mat);
            }

            using var bgrMat = new Mat();
            Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR);
            mat.Dispose();
            return new LoadedImage(sourceName, bgrMat.Clone());
        });
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
