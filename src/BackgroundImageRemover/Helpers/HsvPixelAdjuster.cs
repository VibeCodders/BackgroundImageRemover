using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Utility for adjusting HSV pixel values. Eliminates the duplicated HSV manipulation
/// logic that was previously copy-pasted across HueSatService methods.
/// </summary>
public static class HsvPixelAdjuster
{
    /// <summary>
    /// Adjusts hue, saturation, and value for a single HSV pixel.
    /// </summary>
    /// <param name="h">Hue channel value (0-179 in OpenCV HSV).</param>
    /// <param name="s">Saturation channel value (0-255).</param>
    /// <param name="v">Value channel value (0-255).</param>
    /// <param name="hueShift">Amount to shift hue (wraps around 180).</param>
    /// <param name="satMultiplier">Multiplier for saturation (1.0 = unchanged).</param>
    /// <param name="valMultiplier">Multiplier for value (1.0 = unchanged).</param>
    public static void AdjustPixel(
        byte h, byte s, byte v,
        double hueShift, double satMultiplier, double valMultiplier,
        out byte newH, out byte newS, out byte newV)
    {
        int hue = (int)Math.Round((h + hueShift) % 180);
        if (hue < 0) hue += 180;
        newH = (byte)hue;

        double newSat = s * satMultiplier;
        newS = (byte)Math.Clamp(newSat, 0, 255);

        double newVal = v * valMultiplier;
        newV = (byte)Math.Clamp(newVal, 0, 255);
    }

    /// <summary>
    /// Adjusts hue, saturation, and value for a single HSV pixel in-place using Vec3b.
    /// </summary>
    /// <param name="hsvPixel">The HSV pixel to adjust (modified in place).</param>
    /// <param name="hueShift">Amount to shift hue (wraps around 180).</param>
    /// <param name="satMultiplier">Multiplier for saturation (1.0 = unchanged).</param>
    /// <param name="valMultiplier">Multiplier for value (1.0 = unchanged).</param>
    public static void AdjustPixel(
        ref Vec3b hsvPixel,
        double hueShift, double satMultiplier, double valMultiplier)
    {
        AdjustPixel(
            hsvPixel.Item0, hsvPixel.Item1, hsvPixel.Item2,
            hueShift, satMultiplier, valMultiplier,
            out byte newH, out byte newS, out byte newV);

        hsvPixel = new Vec3b(newH, newS, newV);
    }

    /// <summary>
    /// Adjusts hue, saturation, and value for a single HSV pixel read from a Mat.
    /// </summary>
    /// <param name="hsvMat">The HSV Mat containing the pixel.</param>
    /// <param name="y">Row index of the pixel.</param>
    /// <param name="x">Column index of the pixel.</param>
    /// <param name="hueShift">Amount to shift hue (wraps around 180).</param>
    /// <param name="satMultiplier">Multiplier for saturation (1.0 = unchanged).</param>
    /// <param name="valMultiplier">Multiplier for value (1.0 = unchanged).</param>
    public static void AdjustPixelInMat(
        Mat hsvMat, int y, int x,
        double hueShift, double satMultiplier, double valMultiplier)
    {
        var pixel = hsvMat.Get<Vec3b>(y, x);
        AdjustPixel(ref pixel, hueShift, satMultiplier, valMultiplier);
        hsvMat.Set(y, x, pixel);
    }
}
