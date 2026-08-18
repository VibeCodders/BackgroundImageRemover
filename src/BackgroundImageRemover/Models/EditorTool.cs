namespace BackgroundImageRemover.Models;

/// <summary>
/// Active primary editing tool in the unified Image Editor.
/// </summary>
public enum EditorTool
{
    /// <summary>No tool active; default clean view.</summary>
    None,

    /// <summary>AI and algorithmic background removal (ONNX, SAM, ChromaKey, GrabCut).</summary>
    RemoveBackground,

    /// <summary>Canvas expansion and outpainting/infill methods.</summary>
    Uncrop,

    /// <summary>Direct brush and magic wand pixel retouching.</summary>
    Retouch,

    /// <summary>Color filters and visual adjustments (brightness, contrast, saturation, sharpness).</summary>
    Adjustments,

    /// <summary>Artistic color filters (grayscale, sepia, invert, posterize, emboss, sketch).</summary>
    Filters,

    /// <summary>Geometric transforms (flip, rotate, resize).</summary>
    Transform,

    /// <summary>Places the cutout on a new background with optional drop shadow.</summary>
    Compose,

    /// <summary>Border, rounded corners and transparent padding.</summary>
    Frame,

    /// <summary>Text watermark overlay.</summary>
    Text
}
