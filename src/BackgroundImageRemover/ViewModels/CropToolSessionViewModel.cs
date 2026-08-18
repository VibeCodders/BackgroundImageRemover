using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for cropping (rectangle, aspect presets, margins, auto-trim, straighten).</summary>
public partial class CropToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _sourceBgra;

    public override string ToolBadge => "✂ Crop";
    public override string AccentColor => "#16A34A";

    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private UncropAspectPreset _selectedAspect = UncropAspectPresets.Free;

    [ObservableProperty]
    private double _angle = 0.0;

    [ObservableProperty]
    private int _marginLeft;

    [ObservableProperty]
    private int _marginTop;

    [ObservableProperty]
    private int _marginRight;

    [ObservableProperty]
    private int _marginBottom;

    [ObservableProperty]
    private Rect? _selectedRect;

    [ObservableProperty]
    private string? _statusMessage;

    public CropToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        using var alpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _sourceBgra = _sourceImage.FullBgr.ToBgra(alpha);
        SourceBitmap = _sourceBgra.ToBitmapSource();
        SelectedRect = new Rect(0, 0, _sourceBgra.Width, _sourceBgra.Height);
        RefreshResult();
        StatusMessage = "Drag a rectangle on the left, or use the presets and margins.";
    }

    partial void OnSelectedAspectChanged(UncropAspectPreset value)
    {
        if (_sourceBgra is null) return;
        if (value.Ratio is not { } ratio)
        {
            return;
        }
        SelectedRect = CropService.CenteredRectForAspect(_sourceBgra.Size(), ratio);
        RefreshResult();
    }

    partial void OnAngleChanged(double value) => RefreshResult();

    public void OnRectSelected(Rect rect)
    {
        SelectedRect = rect;
        RefreshResult();
    }

    [RelayCommand]
    private void AutoTrim()
    {
        if (_sourceBgra is null) return;
        SelectedRect = CropService.TrimContentBounds(_sourceBgra);
        RefreshResult();
    }

    [RelayCommand]
    private void ApplyMargins()
    {
        if (_sourceBgra is null) return;
        int width = Math.Max(1, _sourceBgra.Width - MarginLeft - MarginRight);
        int height = Math.Max(1, _sourceBgra.Height - MarginTop - MarginBottom);
        SelectedRect = new Rect(
            Math.Clamp(MarginLeft, 0, _sourceBgra.Width - 1),
            Math.Clamp(MarginTop, 0, _sourceBgra.Height - 1),
            width, height);
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        if (_sourceBgra is null) return;
        SelectedAspect = UncropAspectPresets.Free;
        Angle = 0.0;
        MarginLeft = MarginTop = MarginRight = MarginBottom = 0;
        SelectedRect = new Rect(0, 0, _sourceBgra.Width, _sourceBgra.Height);
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceBgra is null) return;
        using var cropped = CropService.CropRect(_sourceBgra, SelectedRect ?? new Rect(0, 0, _sourceBgra.Width, _sourceBgra.Height));
        using var rotated = Math.Abs(Angle) > 1e-6 ? TransformService.Rotate(cropped, Angle) : cropped.Clone();
        ResultBitmap = rotated.ToBitmapSource();
        IsDirty = true;
    }

    public override Task ApplyAsync()
    {
        if (_sourceBgra is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var cropped = CropService.CropRect(_sourceBgra, SelectedRect ?? new Rect(0, 0, _sourceBgra.Width, _sourceBgra.Height));
        using var rotated = Math.Abs(Angle) > 1e-6 ? TransformService.Rotate(cropped, Angle) : cropped.Clone();
        var (bgr, alpha) = BackgroundCompositingService.SplitBgra(rotated);
        _parentDocument.ApplyToolResult(bgr, alpha, "Crop");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _sourceBgra?.Dispose();
    }
}
