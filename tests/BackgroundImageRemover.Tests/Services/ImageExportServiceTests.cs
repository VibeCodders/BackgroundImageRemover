using System.IO;
using BackgroundImageRemover.Services.ImageIo;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class ImageExportServiceTests
{
    [Fact]
    public async Task ExportPngAsync_WritesDecodableFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.png");
        try
        {
            using var bgra = new Mat(16, 24, MatType.CV_8UC4, new Scalar(10, 20, 30, 200));

            await new ImageExportService().ExportPngAsync(bgra, path);

            using var decoded = Cv2.ImRead(path, ImreadModes.Unchanged);
            Assert.False(decoded.Empty());
            Assert.Equal(24, decoded.Width);
            Assert.Equal(16, decoded.Height);
            Assert.Equal(4, decoded.Channels());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportJpgAsync_WritesDecodableBgrFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.jpg");
        try
        {
            using var bgr = new Mat(16, 24, MatType.CV_8UC3, new Scalar(10, 20, 30));

            await new ImageExportService().ExportJpgAsync(bgr, path);

            using var decoded = Cv2.ImRead(path, ImreadModes.Color);
            Assert.False(decoded.Empty());
            Assert.Equal(24, decoded.Width);
            Assert.Equal(16, decoded.Height);
            Assert.Equal(3, decoded.Channels());

            // A flat-color image survives JPEG compression with its color roughly intact.
            var mean = Cv2.Mean(decoded);
            Assert.True(Math.Abs(mean.Val0 - 10) < 10, $"expected blue ~10, got {mean.Val0}");
            Assert.True(Math.Abs(mean.Val1 - 20) < 10, $"expected green ~20, got {mean.Val1}");
            Assert.True(Math.Abs(mean.Val2 - 30) < 10, $"expected red ~30, got {mean.Val2}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportJpgAsync_QualityIsClampedToValidRange()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.jpg");
        try
        {
            using var bgr = new Mat(8, 8, MatType.CV_8UC3, new Scalar(0, 128, 255));

            // Out-of-range quality values must not throw: they clamp to 1..100.
            await new ImageExportService().ExportJpgAsync(bgr, path, quality: 999);
            await new ImageExportService().ExportJpgAsync(bgr, path, quality: 0);

            using var decoded = Cv2.ImRead(path, ImreadModes.Color);
            Assert.False(decoded.Empty());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
