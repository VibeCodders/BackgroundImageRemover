namespace BackgroundImageRemover.Models;

/// <summary>
/// Active primary editing tool in the unified Image Editor.
/// </summary>
public enum EditorTool
{
    /// <summary>AI and algorithmic background removal (ONNX, SAM, ChromaKey, GrabCut).</summary>
    RemoveBackground,

    /// <summary>Canvas expansion and outpainting/infill methods.</summary>
    Uncrop,

    /// <summary>Direct brush and magic wand pixel retouching.</summary>
    Retouch
}
