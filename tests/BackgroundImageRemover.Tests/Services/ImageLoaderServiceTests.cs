using System.IO;
using BackgroundImageRemover.Services.ImageIo;
using OpenCvSharp;

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
