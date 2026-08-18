namespace BackgroundImageRemover.Models;

/// <summary>How the area added by Uncrop beyond the original image borders gets filled.</summary>
public enum UncropFillMode
{
    Mirror,
    Inpaint,
    SolidColor,
    Replicate,
    Wrap,
    ZoomBlur,
    EdgeGradient,
    PatchSynthesis,

    /// <summary>AI outpainting (LaMa). Not yet wired up — no model source has been chosen.</summary>
    AiOutpaint
}
