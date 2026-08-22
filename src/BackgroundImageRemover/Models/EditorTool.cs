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
    Text,

    /// <summary>Crop with aspect presets, margins, auto-trim and straighten.</summary>
    Crop,

    /// <summary>Freehand selection: draw an outline, keep (or drop) everything inside it.</summary>
    LassoSelect,

    /// <summary>Resize with aspect lock, percent and interpolation.</summary>
    Resize,

    /// <summary>Pixelate or blur a region (or the whole image).</summary>
    Mosaic,

    /// <summary>Composite a second image (logo/sticker) over the document.</summary>
    Overlay,

    /// <summary>Levels adjustment (black point, white point, gamma).</summary>
    Levels,

    /// <summary>Inpaint brush and whole-image repair (dust, scratches, smoothing, detail).</summary>
    Heal,

    /// <summary>Local warps: pinch, bloat, twirl, push.</summary>
    Liquify,

    /// <summary>Four-point perspective correction (keystone/straighten).</summary>
    Perspective,

    /// <summary>Cinematic effects: bloom, glow, light leak, chromatic aberration, bokeh.</summary>
    Fx,

    /// <summary>Tilt-shift miniature effect.</summary>
    TiltShift,

    /// <summary>Samples pixel colors from the image under the cursor for copy/inspection.</summary>
    ColorPicker,

    /// <summary>Selective blur on a painted region or the whole image.</summary>
    Blur,

    /// <summary>Selective sharpening on a painted region or the whole image.</summary>
    Sharpen,

    /// <summary>Adds a vignette (darken/lighten edges) effect.</summary>
    Vignette,

    /// <summary>Renders emoji or text glyphs as decorative overlays on the image.</summary>
    Emoji,

    /// <summary>Arbitrary-angle rotation of the document (with optional canvas expansion).</summary>
    Rotate,

    /// <summary>Adds Gaussian or salt-and-pepper noise to the image.</summary>
    Noise,

    /// <summary>Dodge (lighten) or burn (darken) a painted region.</summary>
    DodgeBurn,

    /// <summary>Adjust hue and saturation of the whole image or a painted region.</summary>
    HueSat,

    /// <summary>Clone stamp: paint pixels copied from a source point.</summary>
    CloneStamp,

    /// <summary>Automatic red-eye removal by clicking on eyes.</summary>
    RedEye,

    /// <summary>Draws vector shapes (rectangle, ellipse, line, arrow) with stroke and fill.</summary>
    Shape,

    /// <summary>Overlays a linear or radial color gradient onto the image.</summary>
    Gradient,

    /// <summary>Replaces pixels matching a target color with a new color.</summary>
    ColorReplace,

    /// <summary>Maps image luminance to a two-color (duotone) palette.</summary>
    Duotone
}
