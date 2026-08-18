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

    /// <summary>Exposure gamma in [0.2, 3.0]. Default 1.0 (no change; &gt;1 brightens, &lt;1 darkens).</summary>
    public double Exposure { get; init; } = 1.0;

    /// <summary>Highlight recovery in [-100, 100]. Positive darkens blown highlights.</summary>
    public double Highlights { get; init; } = 0.0;

    /// <summary>Shadow lift in [-100, 100]. Positive brightens crushed shadows.</summary>
    public double Shadows { get; init; } = 0.0;

    /// <summary>Denoise strength in [0.0, 1.0]. Default 0.0 (no denoising).</summary>
    public double Denoise { get; init; } = 0.0;

    /// <summary>When true, applies automatic contrast (CLAHE) and gray-world white balance first.</summary>
    public bool AutoEnhance { get; init; }

    /// <summary>Vibrance in [-1, 1]: boosts low-saturation colors more than already-saturated ones (0 = off).</summary>
    public double Vibrance { get; init; }

    /// <summary>Clarity in [0, 1]: local contrast via CLAHE, blended with the original (0 = off).</summary>
    public double Clarity { get; init; }

    /// <summary>Fade in [0, 1]: lifts blacks toward mid-gray for a matte film look (0 = off).</summary>
    public double Fade { get; init; }

    /// <summary>Film grain in [0, 1]: additive Gaussian noise amount (0 = off).</summary>
    public double Grain { get; init; }

    /// <summary>Monochrome in [0, 1]: blends toward a grayscale rendition (0 = full color, 1 = B&amp;W).</summary>
    public double Monochrome { get; init; }

    /// <summary>Dehaze in [0, 1]: local contrast equalization plus a slight saturation lift.</summary>
    public double Dehaze { get; init; }

    /// <summary>Soften in [0, 1]: edge-preserving bilateral smoothing (skin smoothing).</summary>
    public double Soften { get; init; }

    /// <summary>Sepia tone in [0, 1]: blends toward a sepia rendition.</summary>
    public double SepiaTone { get; init; }

    /// <summary>Invert amount in [0, 1]: blends toward a color-inverted rendition.</summary>
    public double InvertAmount { get; init; }

    /// <summary>Posterize levels (0 = off, otherwise the number of color levels per channel).</summary>
    public int PosterizeLevels { get; init; }

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
        Math.Abs(SharpenStrength) < 1e-4 &&
        Math.Abs(Exposure - 1.0) < 1e-4 &&
        Math.Abs(Highlights) < 1e-4 &&
        Math.Abs(Shadows) < 1e-4 &&
        Math.Abs(Denoise) < 1e-4 &&
        Math.Abs(Vibrance) < 1e-4 &&
        Math.Abs(Clarity) < 1e-4 &&
        Math.Abs(Fade) < 1e-4 &&
        Math.Abs(Grain) < 1e-4 &&
        Math.Abs(Monochrome) < 1e-4 &&
        Math.Abs(Dehaze) < 1e-4 &&
        Math.Abs(Soften) < 1e-4 &&
        Math.Abs(SepiaTone) < 1e-4 &&
        Math.Abs(InvertAmount) < 1e-4 &&
        PosterizeLevels == 0 &&
        !AutoEnhance;

    public static ImageAdjustments Default { get; } = new();
}

