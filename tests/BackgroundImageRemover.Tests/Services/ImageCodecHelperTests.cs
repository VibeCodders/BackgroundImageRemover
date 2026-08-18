using BackgroundImageRemover.Services.ImageIo;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class ImageCodecHelperTests
{
    [Fact]
    public void EncodeAndDecodePngBase64_RoundTripsAccurately()
    {
        using var original = new Mat(20, 30, MatType.CV_8UC3, new Scalar(40, 80, 160));
        original.Set(5, 5, new Vec3b(10, 20, 30));

        string base64 = ImageCodecHelper.EncodePngBase64(original);
        Assert.False(string.IsNullOrWhiteSpace(base64));

        using var decoded = ImageCodecHelper.DecodePngBase64(base64);
        Assert.Equal(original.Rows, decoded.Rows);
        Assert.Equal(original.Cols, decoded.Cols);
        Assert.Equal(original.Channels(), decoded.Channels());
        Assert.Equal(new Vec3b(10, 20, 30), decoded.At<Vec3b>(5, 5));
    }

    [Fact]
    public void DecodePngBase64_ThrowsOnInvalidBase64OrEmptyBuffer()
    {
        string invalidPng = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        Assert.Throws<InvalidOperationException>(() => ImageCodecHelper.DecodePngBase64(invalidPng));
    }
}
