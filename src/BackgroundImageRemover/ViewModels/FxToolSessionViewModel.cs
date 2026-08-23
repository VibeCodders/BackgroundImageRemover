using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for cinematic/optical effects.</summary>
public partial class FxToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "✨ FX";
    public override string AccentColor => "#C026D3";

    [ObservableProperty]
    private double _glowStrength;

    [ObservableProperty]
    private double _bloomStrength;

    [ObservableProperty]
    private double _lightLeakStrength;

    [ObservableProperty]
    private double _chromaticAberrationStrength;

    [ObservableProperty]
    private int _bokehCount;

    [ObservableProperty]
    private double _bokehSize = 20;

    protected override string OperationName => "FX";

    protected override bool IsEffectActive =>
        GlowStrength + BloomStrength + LightLeakStrength + ChromaticAberrationStrength > 1e-4
        || BokehCount > 0;

    public FxToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Add glow, bloom, light leaks, aberration or bokeh.")
    {
        RefreshPreview();
    }

    partial void OnGlowStrengthChanged(double value) => RefreshPreview();
    partial void OnBloomStrengthChanged(double value) => RefreshPreview();
    partial void OnLightLeakStrengthChanged(double value) => RefreshPreview();
    partial void OnChromaticAberrationStrengthChanged(double value) => RefreshPreview();
    partial void OnBokehCountChanged(int value) => RefreshPreview();
    partial void OnBokehSizeChanged(double value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
    {
        bool owns = true;
        var result = bgr.Clone();
        result = result.SafeChainWithCatch(r => BloomStrength > 1e-4 ? FxService.Bloom(r, BloomStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => GlowStrength > 1e-4 ? FxService.Glow(r, GlowStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => LightLeakStrength > 1e-4 ? FxService.LightLeak(r, LightLeakStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => ChromaticAberrationStrength > 1e-4 ? FxService.ChromaticAberration(r, ChromaticAberrationStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => BokehCount > 0 ? FxService.Bokeh(r, BokehCount, BokehSize) : r, ref owns);
        return result;
    }

    protected override void OnResetDefaults()
    {
        GlowStrength = BloomStrength = LightLeakStrength = ChromaticAberrationStrength = 0;
        BokehCount = 0;
        BokehSize = 20;
    }
}
