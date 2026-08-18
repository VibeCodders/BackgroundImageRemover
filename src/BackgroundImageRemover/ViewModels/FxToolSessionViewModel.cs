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
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

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
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
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
        var result = _sourceImage!.FullBgr.Clone();
        try
        {
            if (BloomStrength > 1e-4)
            {
                var bloomed = FxService.Bloom(result, BloomStrength);
                result.Dispose();
                result = bloomed;
            }
            if (GlowStrength > 1e-4)
            {
                var glowed = FxService.Glow(result, GlowStrength);
                result.Dispose();
                result = glowed;
            }
            if (LightLeakStrength > 1e-4)
            {
                var leaked = FxService.LightLeak(result, LightLeakStrength);
                result.Dispose();
                result = leaked;
            }
            if (ChromaticAberrationStrength > 1e-4)
            {
                var aberrated = FxService.ChromaticAberration(result, ChromaticAberrationStrength);
                result.Dispose();
                result = aberrated;
            }
            if (BokehCount > 0)
            {
                var bokeh = FxService.Bokeh(result, BokehCount, BokehSize);
                result.Dispose();
                result = bokeh;
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
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
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var result = BuildResult();
            _parentDocument.ApplyToolResult(result, _workingAlpha.Clone(), "FX");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
