using BackgroundImageRemover.Models;
using BackgroundImageRemover.ViewModels.StrategyViewModels;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// The per-strategy settings VMs and mask/refinement knobs needed to build a <see cref="StrategyContext"/>.
/// Implemented by both <see cref="ViewModels.DocumentViewModel"/> (the GIMP-style inline canvas
/// preview) and <see cref="ViewModels.BackgroundRemoverToolSessionViewModel"/> (the dedicated
/// tool tab) so <see cref="StrategyContextBuilder"/> has one place to turn "current settings"
/// into a <see cref="StrategyContext"/>, instead of each view model re-implementing the same
/// switch on <see cref="StrategyKind"/>.
/// </summary>
public interface IStrategyParameterSource
{
    StrategyKind SelectedStrategy { get; }
    ChromaKeyStrategyViewModel ChromaKey { get; }
    GrabCutStrategyViewModel GrabCut { get; }
    OnnxStrategyViewModel Onnx { get; }
    SamStrategyViewModel Sam { get; }
    FloodFillStrategyViewModel FloodFill { get; }
    KMeansStrategyViewModel KMeans { get; }
    MagicWandStrategyViewModel MagicWand { get; }
    InpaintStrategyViewModel Inpaint { get; }

    bool InvertMask { get; }
    double MaskFeatherPixels { get; }
    int MaskExpandPixels { get; }
    double MaskBlurPixels { get; }
    int MinComponentAreaPixels { get; }
    double MaskGamma { get; }
    double MaskHardness { get; }
    int MaskThreshold { get; }
    double DespillStrength { get; }
    int MaskMedianKernel { get; }
    int MaskBilateralKernel { get; }
    bool MaskClahe { get; }

    /// <summary>Extra cleanup-pass settings; the inline canvas preview has no UI for these and
    /// always reports the record's own no-op defaults (0 / false).</summary>
    int DespeckleKernelSize { get; }
    int FillHolesKernelSize { get; }
    int SmoothEdgesKernelSize { get; }
    bool KeepLargestComponent { get; }
}
