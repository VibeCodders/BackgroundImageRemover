using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.ImageIo;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Helpers;

public class ExifOrientationHelperTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void ReadOrientation_ReadsEmbeddedExifTag(int orientation)
    {
        using var bgr = new Mat(10, 20, MatType.CV_8UC3, new Scalar(0, 128, 255));
        Cv2.ImEncode(".jpg", bgr, out var jpeg);
        var withExif = WrapWithExifOrientation(jpeg, orientation);

        Assert.Equal(orientation, ExifOrientationHelper.ReadOrientation(withExif));
    }

    [Fact]
    public void ReadOrientation_PlainJpeg_ReturnsOne()
    {
        using var bgr = new Mat(10, 20, MatType.CV_8UC3, new Scalar(0, 128, 255));
        Cv2.ImEncode(".jpg", bgr, out var jpeg);

        Assert.Equal(1, ExifOrientationHelper.ReadOrientation(jpeg));
    }

    [Fact]
    public void ReadOrientation_NonJpegBytes_ReturnsOne()
    {
        Assert.Equal(1, ExifOrientationHelper.ReadOrientation(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        Assert.Equal(1, ExifOrientationHelper.ReadOrientation(Array.Empty<byte>()));
        Assert.Equal(1, ExifOrientationHelper.ReadOrientation(null!));
    }

    [Fact]
    public void ReadOrientation_TiffHeader_ReturnsTag()
    {
        // Minimal little-endian TIFF header + IFD0 with a single Orientation=3 entry.
        var tiff = new byte[]
        {
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // header, IFD0 at 8
            0x01, 0x00,                                              // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,          // tag 274, SHORT, count 1
            0x03, 0x00, 0x00, 0x00,                                  // value = 3
            0x00, 0x00, 0x00, 0x00                                   // no next IFD
        };

        Assert.Equal(3, ExifOrientationHelper.ReadOrientation(tiff));
    }

    /// <summary>
    /// Verifies the geometric transform for every orientation using a tiny 2x3 image with
    /// uniquely colored corners, checking both the resulting dimensions and the top-left pixel.
    /// </summary>
    [Theory]
    [InlineData(1, 2, 3, 0, 0, 255)] // as-is: A stays top-left
    [InlineData(2, 2, 3, 0, 255, 0)] // mirror left-right: B moves to top-left
    [InlineData(3, 2, 3, 0, 255, 255)] // rotate 180: F moves to top-left
    [InlineData(4, 2, 3, 0, 0, 0)]    // mirror top-bottom: E moves to top-left
    [InlineData(5, 3, 2, 0, 0, 255)]  // transpose: A stays top-left, dims swap
    [InlineData(6, 3, 2, 0, 0, 0)]    // rotate 90 CW: E moves to top-left, dims swap
    [InlineData(7, 3, 2, 0, 0, 0)]    // transverse: E moves to top-left, dims swap
    [InlineData(8, 3, 2, 0, 255, 0)]  // rotate 90 CCW: B moves to top-left, dims swap
    public void ApplyOrientation_MatchesCameraTable(
        int orientation, int expectedWidth, int expectedHeight, byte expectedB, byte expectedG, byte expectedR)
    {
        // 2 cols x 3 rows:  A B / C D / E F
        using var source = new Mat(3, 2, MatType.CV_8UC3);
        source.Set(0, 0, new Vec3b(0, 0, 255));     // A = red
        source.Set(0, 1, new Vec3b(0, 255, 0));     // B = green
        source.Set(1, 0, new Vec3b(255, 0, 0));     // C = blue
        source.Set(1, 1, new Vec3b(255, 255, 255)); // D = white
        source.Set(2, 0, new Vec3b(0, 0, 0));       // E = black
        source.Set(2, 1, new Vec3b(0, 255, 255));   // F = yellow

        using var result = ExifOrientationHelper.ApplyOrientation(source, orientation);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        var topLeft = result.At<Vec3b>(0, 0);
        Assert.Equal(new Vec3b(expectedB, expectedG, expectedR), topLeft);
    }

    [Fact]
    public async Task ImageLoader_AppliesExifOrientationOnLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"exif_{Guid.NewGuid():N}.jpg");
        try
        {
            // 20 wide x 10 tall, blue everywhere except a red block at the bottom-left.
            using var source = new Mat(10, 20, MatType.CV_8UC3, new Scalar(255, 0, 0));
            Cv2.Rectangle(source, new Rect(0, 7, 3, 3), new Scalar(0, 0, 255), -1);

            Cv2.ImEncode(".jpg", source, out var jpeg);
            File.WriteAllBytes(path, WrapWithExifOrientation(jpeg, orientation: 6));

            using var loaded = await new ImageLoaderService().LoadAsync(path);

            // Rotated 90 CW: dimensions swap and the bottom-left block lands top-left
            // (red-dominant, allowing for JPEG compression smearing).
            Assert.Equal(10, loaded.FullBgr.Width);
            Assert.Equal(20, loaded.FullBgr.Height);
            var topLeft = loaded.FullBgr.At<Vec3b>(0, 0);
            Assert.True(topLeft.Item2 > topLeft.Item0, $"expected red-dominant top-left after rotation, got {topLeft}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Prepends an APP1 Exif segment with the given orientation tag to a JPEG.</summary>
    private static byte[] WrapWithExifOrientation(byte[] jpeg, int orientation)
    {
        // TIFF header (little-endian) + IFD0 with one Orientation (0x0112, SHORT) entry.
        var exif = new List<byte>
        {
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        var app1 = new List<byte> { 0xFF, 0xE1 };
        // JPEG segment length includes the 2 length bytes: "Exif\0\0" (6) + TIFF data + 2.
        int len = exif.Count + 8;
        app1.Add((byte)(len >> 8));
        app1.Add((byte)(len & 0xFF));
        app1.AddRange(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00 });
        app1.AddRange(exif);

        // Insert the APP1 segment right after the SOI marker, as cameras do.
        var result = new byte[jpeg.Length + app1.Count];
        Array.Copy(jpeg, 0, result, 0, 2);
        app1.CopyTo(result, 2);
        Array.Copy(jpeg, 2, result, 2 + app1.Count, jpeg.Length - 2);
        return result;
    }
}
