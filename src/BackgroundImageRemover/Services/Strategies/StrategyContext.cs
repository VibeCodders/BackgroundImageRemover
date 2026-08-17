using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
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

    /// <summary>GrabCut: iteration count, identical for preview and full-res so exports match the preview.</summary>
    public int GrabCutIterations { get; init; } = 3;

    /// <summary>Chroma Key: sampled/detected background color (BGR).</summary>
    public Vec3b? ChromaKeyColor { get; init; }

    /// <summary>Chroma Key: tolerance, 0-100.</summary>
    public double ChromaKeyTolerance { get; init; } = 20;

    /// <summary>Remove the background color's cast from semi-transparent edge pixels before returning the result.</summary>
    public bool DecontaminateEdges { get; init; } = true;

    /// <summary>Neighborhood radius (px) used to estimate the local background color when decontaminating.</summary>
    public int DecontaminationEstimateRadius { get; init; } = ColorDecontaminator.DefaultEstimateRadius;

    /// <summary>ONNX: which model to run inference with.</summary>
    public OnnxModelKind OnnxModel { get; init; } = OnnxModelKind.U2NetP;

    /// <summary>ONNX: edge feather amount in pixels applied to the mask.</summary>
    public int OnnxFeatherPixels { get; init; } = 2;

    /// <summary>Applies guided-filter alpha matting refinement to the computed mask before returning it.</summary>
    public bool EnableAlphaMatting { get; init; }

    /// <summary>SAM: the clicked foreground point, in the coordinate space of the Mat being processed.</summary>
    public Point? SamPromptPoint { get; init; }

    /// <summary>SAM: precomputed image embedding (a <see cref="Services.Sam.SamEmbedding"/>), shared across clicks/output sizes.</summary>
    public object? SamEmbedding { get; init; }
}
