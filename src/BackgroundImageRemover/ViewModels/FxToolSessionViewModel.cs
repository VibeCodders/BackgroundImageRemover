using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for cinematic/optical effects.</summary>
public partial class FxToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "✨ FX";
    public override string AccentColor => "#C026D3";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

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

    [ObservableProperty]
    private string? _statusMessage;

    public FxToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        RefreshResult();
        StatusMessage = "Add glow, bloom, light leaks, aberration or bokeh.";
    }

    partial void OnGlowStrengthChanged(double value) => RefreshResult();
    partial void OnBloomStrengthChanged(double value) => RefreshResult();
    partial void OnLightLeakStrengthChanged(double value) => RefreshResult();
    partial void OnChromaticAberrationStrengthChanged(double value) => RefreshResult();
    partial void OnBokehCountChanged(int value) => RefreshResult();
    partial void OnBokehSizeChanged(double value) => RefreshResult();

    private Mat BuildResult()
    {
        bool owns = true;
        var result = _sourceImage!.FullBgr.Clone();
        result = result.SafeChainWithCatch(r => BloomStrength > 1e-4 ? FxService.Bloom(r, BloomStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => GlowStrength > 1e-4 ? FxService.Glow(r, GlowStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => LightLeakStrength > 1e-4 ? FxService.LightLeak(r, LightLeakStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => ChromaticAberrationStrength > 1e-4 ? FxService.ChromaticAberration(r, ChromaticAberrationStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => BokehCount > 0 ? FxService.Bokeh(r, BokehCount, BokehSize) : r, ref owns);
        return result;
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = BuildResult();
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = GlowStrength + BloomStrength + LightLeakStrength + ChromaticAberrationStrength > 1e-4 || BokehCount > 0;
    }

    [RelayCommand]
    private void Reset()
    {
        GlowStrength = BloomStrength = LightLeakStrength = ChromaticAberrationStrength = 0;
        BokehCount = 0;
        BokehSize = 20;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        Mat? result = null;
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            result = BuildResult();
        }
        ApplyAndClose(result, "FX");
        return Task.CompletedTask;
    }
}
