using System.IO;
using BackgroundImageRemover.Helpers;
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
/// treated as a fresh opaque photo). EXIF orientation is read from JPEG/TIFF files and
/// applied automatically, so phone-camera photos open right-side up.
/// </summary>
public sealed class ImageLoaderService : IImageLoaderService
{
    public Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not read image: {filePath}", ex);
            }

            int orientation = ExifOrientationHelper.ReadOrientation(bytes);

            // Peek at the raw channel count so a transparent PNG (4 channels) can be
            // decoded with its alpha intact.
            using (var probe = Cv2.ImDecode(bytes, ImreadModes.Unchanged))
            {
                if (probe.Empty())
                {
                    throw new InvalidOperationException($"Could not decode image: {filePath}");
                }
                if (probe.Channels() != 4)
                {
                    return LoadWithoutAlpha(bytes, filePath, orientation);
                }
            }

            using var mat = Cv2.ImDecode(bytes, ImreadModes.Unchanged);
            if (mat.Empty())
            {
                throw new InvalidOperationException($"Could not decode image: {filePath}");
            }

            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(mat);
            if (orientation != 1)
            {
                // Ownership of the rotated Mats transfers to the LoadedImage.
                var orientedBgr = ExifOrientationHelper.ApplyOrientation(bgr, orientation);
                var orientedAlpha = ExifOrientationHelper.ApplyOrientation(alpha, orientation);
                bgr.Dispose();
                alpha.Dispose();
                return new LoadedImage(filePath, orientedBgr, orientedAlpha);
            }
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

    private static LoadedImage LoadWithoutAlpha(byte[] bytes, string filePath, int orientation)
    {
        // OpenCV applies EXIF orientation on JPEG decode by default, so decode the raw pixels
        // and let ExifOrientationHelper be the single source of truth (otherwise photos get
        // rotated twice). The probe below uses ImreadModes.Unchanged, which already implies
        // IgnoreOrientation, so the two paths stay consistent.
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color | ImreadModes.IgnoreOrientation);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException($"Could not decode image: {filePath}");
        }

        if (orientation != 1)
        {
            // Ownership of the rotated Mat transfers to the LoadedImage.
            var oriented = ExifOrientationHelper.ApplyOrientation(mat, orientation);
            mat.Dispose();
            return new LoadedImage(filePath, oriented);
        }

        return new LoadedImage(filePath, mat);
    }
}
