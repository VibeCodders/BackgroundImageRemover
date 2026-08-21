using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for adding vignette (darken/lighten edges) effects.</summary>
public partial class VignetteToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "🔳 Vignette";
    public override string AccentColor => "#A78BFA";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _strength = 0.3;

    [ObservableProperty]
    private double _roundness = 0.5;

    [ObservableProperty]
    private double _feather = 0.5;

    [ObservableProperty]
    private bool _invert;

    [ObservableProperty]
    private string? _statusMessage;

    public VignetteToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        RefreshResult();
        StatusMessage = "Adjust vignette strength, roundness and feather.";
    }

    partial void OnStrengthChanged(double value) => RefreshResult();
    partial void OnRoundnessChanged(double value) => RefreshResult();
    partial void OnFeatherChanged(double value) => RefreshResult();
    partial void OnInvertChanged(bool value) => RefreshResult();

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        using var result = VignetteService.Apply(_sourceImage.FullBgr, Strength, Roundness, Feather, Invert);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = Strength > 1e-4;
    }

    [RelayCommand]
    private void Reset()
    {
        Strength = 0.3;
        Roundness = 0.5;
        Feather = 0.5;
        Invert = false;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        Mat? result = null;
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            result = VignetteService.Apply(_sourceImage.FullBgr, Strength, Roundness, Feather, Invert);
        }
        ApplyAndClose(result, "Vignette");
        return Task.CompletedTask;
    }
}
