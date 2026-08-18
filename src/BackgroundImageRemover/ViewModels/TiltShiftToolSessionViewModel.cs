using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for the tilt-shift / miniature effect.</summary>
public partial class TiltShiftToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

    public override string ToolBadge => "📐 Tilt-Shift";
    public override string AccentColor => "#4F46E5";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _focusCenter = 0.5;

    [ObservableProperty]
    private double _focusWidth = 0.35;

    [ObservableProperty]
    private double _blurRadius = 12;

    [ObservableProperty]
    private bool _vertical;

    [ObservableProperty]
    private double _saturationBoost = 0.3;

    [ObservableProperty]
    private string? _statusMessage;

    public TiltShiftToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
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
        StatusMessage = "Adjust the focus band, blur and saturation.";
    }

    partial void OnFocusCenterChanged(double value) => RefreshResult();
    partial void OnFocusWidthChanged(double value) => RefreshResult();
    partial void OnBlurRadiusChanged(double value) => RefreshResult();
    partial void OnVerticalChanged(bool value) => RefreshResult();
    partial void OnSaturationBoostChanged(double value) => RefreshResult();

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = TiltShiftService.Apply(
            _sourceImage.FullBgr, FocusCenter, FocusWidth, BlurRadius, Vertical, SaturationBoost);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = BlurRadius > 0 || Math.Abs(SaturationBoost) > 1e-4;
    }

    [RelayCommand]
    private void Reset()
    {
        FocusCenter = 0.5;
        FocusWidth = 0.35;
        BlurRadius = 12;
        Vertical = false;
        SaturationBoost = 0.3;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var result = TiltShiftService.Apply(
                _sourceImage.FullBgr, FocusCenter, FocusWidth, BlurRadius, Vertical, SaturationBoost);
            _parentDocument.ApplyToolResult(result, _workingAlpha.Clone(), "Tilt-Shift");
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
