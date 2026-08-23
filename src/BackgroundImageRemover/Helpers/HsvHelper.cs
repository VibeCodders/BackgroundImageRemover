using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Helper class for common HSV color space operations.
/// Reduces duplication across services that need to convert between color spaces
/// and adjust HSV values.
/// </summary>
public static class HsvHelper
{
    /// <summary>
    /// Converts a BGR image to HSV color space.
    /// </summary>
    /// <param name="bgr">Input BGR image.</param>
    /// <returns>HSV image (caller owns the returned Mat).</returns>
    public static Mat BgrToHsv(Mat bgr)
    {
        var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        return hsv;
    }

    /// <summary>
    /// Converts an HSV image back to BGR color space.
    /// </summary>
    /// <param name="hsv">Input HSV image.</param>
    /// <returns>BGR image (caller owns the returned Mat).</returns>
    public static Mat HsvToBgr(Mat hsv)
    {
        var bgr = new Mat();
        Cv2.CvtColor(hsv, bgr, ColorConversionCodes.HSV2BGR);
        return bgr;
    }

    /// <summary>
    /// Converts a single BGR pixel to HSV.
    /// </summary>
    /// <param name="bgrPixel">BGR pixel value.</param>
    /// <returns>HSV pixel value.</returns>
    public static Vec3b BgrToHsv(Vec3b bgrPixel)
    {
        using var src = new Mat(1, 1, MatType.CV_8UC3, new Scalar(bgrPixel[0], bgrPixel[1], bgrPixel[2]));
        using var hsv = BgrToHsv(src);
        return hsv.Get<Vec3b>(0, 0);
    }

    /// <summary>
    /// Converts a single HSV pixel to BGR.
    /// </summary>
    /// <param name="hsvPixel">HSV pixel value.</param>
    /// <returns>BGR pixel value.</returns>
    public static Vec3b HsvToBgr(Vec3b hsvPixel)
    {
        using var src = new Mat(1, 1, MatType.CV_8UC3, new Scalar(hsvPixel[0], hsvPixel[1], hsvPixel[2]));
        using var bgr = HsvToBgr(src);
        return bgr.Get<Vec3b>(0, 0);
    }

    /// <summary>
    /// Extracts the luminance (V channel) from a BGR image as a grayscale Mat.
    /// </summary>
    /// <param name="bgr">Input BGR image.</param>
    /// <returns>Grayscale image representing luminance (caller owns the returned Mat).</returns>
    public static Mat ExtractLuminance(Mat bgr)
    {
        using var hsv = BgrToHsv(bgr);
        var channels = new Mat[3];
        Cv2.Split(hsv, out channels);
        try
        {
            // Return a clone of the V channel (luminance)
            return channels[2].Clone();
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }
}
