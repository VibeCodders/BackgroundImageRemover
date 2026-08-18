using OpenCvSharp;

namespace BackgroundImageRemover.Services.ImageIo;

/// <summary>
/// Utility methods for encoding/decoding OpenCV Mat buffers to/from Base64 or byte arrays.
/// </summary>
public static class ImageCodecHelper
{
    /// <summary>
    /// Encodes a Mat to Base64 formatted PNG.
    /// </summary>
    public static string EncodePngBase64(Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var buffer);
        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Decodes a Base64 string into a Mat.
    /// </summary>
    public static Mat DecodePngBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var mat = Cv2.ImDecode(bytes, ImreadModes.Unchanged);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException("Could not decode the embedded image buffer.");
        }
        return mat;
    }
}
