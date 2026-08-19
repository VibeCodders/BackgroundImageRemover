using System.IO;
using BackgroundImageRemover.Services.ImageIo;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.Tests.Services;

public class ImageLoaderServiceTests
{
    [Fact]
    public async Task LoadAsync_PreservesAlphaChannel_WhenSourceHasTransparency()
    {
        using var bgra = new Mat(50, 40, MatType.CV_8UC4, new Scalar(10, 20, 30, 0));
        using var opaqueRoi = new Mat(bgra, new Rect(5, 5, 10, 10));
        opaqueRoi.SetTo(new Scalar(200, 100, 50, 255));

        var path = Path.Combine(Path.GetTempPath(), $"cutout_{Guid.NewGuid():N}.png");
        try
        {
            Assert.True(Cv2.ImWrite(path, bgra));

            var loader = new ImageLoaderService();
            using var loaded = await loader.LoadAsync(path);

            Assert.NotNull(loaded.FullAlpha);
            Assert.Equal(50, loaded.FullBgr.Rows);
            Assert.Equal(40, loaded.FullBgr.Cols);
            Assert.Equal(3, loaded.FullBgr.Channels());
            // Fully transparent pixels stay 0, opaque ones stay 255.
            Assert.Equal(0, loaded.FullAlpha!.At<byte>(0, 0));
            Assert.Equal(255, loaded.FullAlpha.At<byte>(7, 7));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadFromBitmapSourceAsync_WithAnUnfrozenSourceOwnedByAnotherThread_Succeeds()
    {
        // Clipboard paste hands the loader a BitmapSource created on the UI thread, unfrozen.
        // The loader decodes on a worker thread, and WPF forbids touching an unfrozen
        // Freezable from a non-owner thread -- so the loader must make it shareable on the
        // caller's thread first. Regression: "The calling thread cannot access this object
        // because a different thread owns it".
        var loader = new ImageLoaderService();

        using var bgra = new Mat(8, 6, MatType.CV_8UC4, new Scalar(10, 20, 30, 255));
        var source = bgra.ToBitmapSource(); // created on this (owner) thread, unfrozen
        Assert.False(source.IsFrozen);

        using var loaded = await loader.LoadFromBitmapSourceAsync(source, "clipboard.png");

        Assert.Equal(6, loaded.FullBgr.Cols);
        Assert.Equal(8, loaded.FullBgr.Rows);
        Assert.Equal(3, loaded.FullBgr.Channels());
        Assert.NotNull(loaded.FullAlpha);
    }

    [Fact]
    public async Task LoadAsync_HasNoAlpha_ForOpaqueImage()
    {
        using var bgr = new Mat(20, 30, MatType.CV_8UC3, new Scalar(1, 2, 3));

        var path = Path.Combine(Path.GetTempPath(), $"opaque_{Guid.NewGuid():N}.png");
        try
        {
            Assert.True(Cv2.ImWrite(path, bgr));

            var loader = new ImageLoaderService();
            using var loaded = await loader.LoadAsync(path);

            Assert.Null(loaded.FullAlpha);
            Assert.Equal(3, loaded.FullBgr.Channels());
            Assert.Equal(20, loaded.FullBgr.Rows);
            Assert.Equal(30, loaded.FullBgr.Cols);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
