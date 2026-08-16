using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Strategy-specific parameters passed into a background-removal run. Fields not relevant
/// to the active strategy are left null/default.
/// </summary>
public sealed record StrategyContext
{
    /// <summary>GrabCut: subject rectangle in the coordinate space of the Mat being processed.</summary>
    public Rect? GrabCutRect { get; init; }

    /// <summary>GrabCut: iteration count (lower for preview, higher for full-res).</summary>
    public int GrabCutIterations { get; init; } = 3;

    /// <summary>Chroma Key: sampled/detected background color (BGR).</summary>
    public Vec3b? ChromaKeyColor { get; init; }

    /// <summary>Chroma Key: tolerance, 0-100.</summary>
    public double ChromaKeyTolerance { get; init; } = 20;

    /// <summary>Chroma Key: neutralize the background color's cast on semi-transparent edge pixels.</summary>
    public bool ChromaKeySpillSuppression { get; init; } = true;

    /// <summary>ONNX: which model to run inference with.</summary>
    public OnnxModelKind OnnxModel { get; init; } = OnnxModelKind.U2NetP;

    /// <summary>ONNX: edge feather amount in pixels applied to the mask.</summary>
    public int OnnxFeatherPixels { get; init; } = 2;

    /// <summary>Applies guided-filter alpha matting refinement to the computed mask before returning it.</summary>
    public bool EnableAlphaMatting { get; init; }
}
