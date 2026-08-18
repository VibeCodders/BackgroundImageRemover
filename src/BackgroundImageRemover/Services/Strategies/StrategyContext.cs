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

    /// <summary>GrabCut: edge feather amount in pixels applied to the mask, scaled with resolution like <see cref="OnnxFeatherPixels"/> so exports keep the same relative softness as the preview.</summary>
    public int GrabCutFeatherPixels { get; init; } = 2;

    /// <summary>GrabCut: foreground scribble mask (non-zero = certain foreground), in the coordinate space of the Mat being processed. Optional, like <see cref="GrabCutRect"/>.</summary>
    public Mat? GrabCutForegroundScribble { get; init; }

    /// <summary>GrabCut: background scribble mask (non-zero = certain background), in the coordinate space of the Mat being processed. Optional, like <see cref="GrabCutRect"/>.</summary>
    public Mat? GrabCutBackgroundScribble { get; init; }

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

    /// <summary>FloodFill: max Lab color distance from the border seed for a pixel to be flooded as background.</summary>
    public double FloodFillTolerance { get; init; } = 20;

    /// <summary>KMeans: number of color clusters to split the image into.</summary>
    public int KMeansClusters { get; init; } = 4;

    /// <summary>KMeans: a cluster is treated as background when at least this fraction of its pixels sit on the image border.</summary>
    public double KMeansBorderFraction { get; init; } = 0.25;

    /// <summary>MagicWand: the clicked seed point, in the coordinate space of the Mat being processed.</summary>
    public Point? MagicWandSeed { get; init; }

    /// <summary>MagicWand: max Lab color distance from the seed for a pixel to be flooded as background.</summary>
    public double MagicWandTolerance { get; init; } = 20;

    /// <summary>Inverts the computed mask (keeps the background, removes the subject) before compositing.</summary>
    public bool InvertMask { get; init; }

    /// <summary>Feathers the final mask by this many pixels (0 disables it). Scaled with resolution like the other feather fields.</summary>
    public int MaskFeatherPixels { get; init; }

    /// <summary>Removes small foreground specks via morphological open (0 disables it).</summary>
    public int DespeckleKernelSize { get; init; }

    /// <summary>Fills small background holes via morphological close (0 disables it).</summary>
    public int FillHolesKernelSize { get; init; }

    /// <summary>Smooths jagged mask edges via a median filter (0 disables it).</summary>
    public int SmoothEdgesKernelSize { get; init; }

    /// <summary>Keeps only the largest connected foreground region, dropping stray islands.</summary>
    public bool KeepLargestComponent { get; init; }

    /// <summary>Dilates (&gt;0) or erodes (&lt;0) the mask by this many pixels to grow/shrink the subject.</summary>
    public int MaskExpandPixels { get; init; }

    /// <summary>Gaussian-blurs the mask by this sigma (0 disables it) to soften edges.</summary>
    public double MaskBlurPixels { get; init; }

    /// <summary>Drops foreground components smaller than this many pixels (0 disables it).</summary>
    public int MinComponentAreaPixels { get; init; }

    /// <summary>Gamma applied to the mask alpha (default 1.0; &gt;1 sharpens the cutout, &lt;1 expands the soft edge).</summary>
    public double MaskGamma { get; init; } = 1.0;

    /// <summary>Hardens soft mask edges (0 = original, 1 = fully hardened with a smoothstep curve).</summary>
    public double MaskHardness { get; init; }
}
