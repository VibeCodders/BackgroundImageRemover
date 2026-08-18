namespace BackgroundImageRemover.Models;

/// <summary>
/// Parameters for color and detail adjustments on an image.
/// </summary>
public record ImageAdjustments
{
    /// <summary>Brightness offset in [-100, 100]. Default 0 (no change).</summary>
    public double Brightness { get; init; } = 0.0;

    /// <summary>Contrast multiplier in [0.1, 3.0]. Default 1.0 (no change).</summary>
    public double Contrast { get; init; } = 1.0;

    /// <summary>Saturation multiplier in [0.0, 3.0]. Default 1.0 (no change, 0 = grayscale).</summary>
    public double Saturation { get; init; } = 1.0;

    /// <summary>Hue rotation in degrees in [-180, 180]. Default 0 (no change).</summary>
    public double HueShift { get; init; } = 0.0;

    /// <summary>Color Temperature offset in [-100, 100]. Negative = cooler (blue), Positive = warmer (amber/red).</summary>
    public double Temperature { get; init; } = 0.0;

    /// <summary>Tint offset in [-100, 100]. Negative = green, Positive = magenta.</summary>
    public double Tint { get; init; } = 0.0;

    /// <summary>Vignette darkening amount in [0.0, 1.0]. Default 0.0 (no vignette).</summary>
    public double Vignette { get; init; } = 0.0;

    /// <summary>Gaussian blur radius in [0, 50]. Default 0 (no blur).</summary>
    public int BlurRadius { get; init; } = 0;

    /// <summary>Unsharp mask / sharpening strength in [0.0, 3.0]. Default 0.0 (no sharpening).</summary>
    public double SharpenStrength { get; init; } = 0.0;

    /// <summary>Returns true if all parameters are at neutral/identity values.</summary>
    public bool IsIdentity =>
        Math.Abs(Brightness) < 1e-4 &&
        Math.Abs(Contrast - 1.0) < 1e-4 &&
        Math.Abs(Saturation - 1.0) < 1e-4 &&
        Math.Abs(HueShift) < 1e-4 &&
        Math.Abs(Temperature) < 1e-4 &&
        Math.Abs(Tint) < 1e-4 &&
        Math.Abs(Vignette) < 1e-4 &&
        BlurRadius == 0 &&
        Math.Abs(SharpenStrength) < 1e-4;

    public static ImageAdjustments Default { get; } = new();
}

