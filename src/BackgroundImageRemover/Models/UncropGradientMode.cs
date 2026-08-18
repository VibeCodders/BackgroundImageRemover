namespace BackgroundImageRemover.Models;

/// <summary>Type of edge gradient generation for Uncrop fill.</summary>
public enum UncropGradientMode
{
    /// <summary>Linearly interpolates colors sampled from the 4 image borders toward the canvas edges.</summary>
    PerEdgeSplay,

    /// <summary>Fades smoothly from the image edges to a target background or custom color.</summary>
    FadeToColor,

    /// <summary>Bilinear 4-corner ambient gradient across the outer border.</summary>
    FourCorners
}
