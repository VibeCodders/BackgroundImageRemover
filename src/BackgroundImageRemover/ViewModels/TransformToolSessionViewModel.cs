using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for geometric transforms (flip, rotate, resize).</summary>
public partial class TransformToolSessionViewModel : BgraToolSessionViewModelBase
{
    public override string ToolBadge => "↻ Transform";
    public override string AccentColor => "#2563EB";

    [ObservableProperty]
    private double _angle = 0.0;

    [ObservableProperty]
    private double _scalePercent = 100.0;

    [ObservableProperty]
    private double _skewX;

    [ObservableProperty]
    private double _skewY;

    [ObservableProperty]
    private int _exactWidth;

    [ObservableProperty]
    private int _exactHeight;

    [ObservableProperty]
    private double _cropAspectRatio = 1.0;

    [ObservableProperty]
    private int _padLeft;

    [ObservableProperty]
    private int _padTop;

    [ObservableProperty]
    private int _padRight;

    [ObservableProperty]
    private int _padBottom;

    [ObservableProperty]
    private int _fitWidth = 1024;

    [ObservableProperty]
    private int _fitHeight = 1024;

    [ObservableProperty]
    private int _centerCropWidth;

    [ObservableProperty]
    private int _centerCropHeight;

    [ObservableProperty]
    private int _tileWidth;

    [ObservableProperty]
    private int _tileHeight;

    public TransformToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitWorkingBgra();
        ExactWidth = WorkingBgra!.Width;
        ExactHeight = WorkingBgra.Height;
        FitWidth = 1024;
        FitHeight = 1024;
        CenterCropWidth = WorkingBgra.Width;
        CenterCropHeight = WorkingBgra.Height;
        TileWidth = WorkingBgra.Width;
        TileHeight = WorkingBgra.Height;
        RefreshPreview();
        StatusMessage = "Apply flips, rotations, scaling, skew, crop, trim, padding, fit, tile and auto-straighten.";
    }

    private void RefreshPreview()
    {
        RefreshBgraPreview();
        IsDirty = true;
    }

    [RelayCommand]
    private void FlipHorizontal() => ApplyTransform(TransformService.FlipHorizontal);

    [RelayCommand]
    private void FlipVertical() => ApplyTransform(TransformService.FlipVertical);

    [RelayCommand]
    private void Rotate90Clockwise() => ApplyTransform(TransformService.Rotate90Clockwise);

    [RelayCommand]
    private void Rotate90CounterClockwise() => ApplyTransform(TransformService.Rotate90CounterClockwise);

    [RelayCommand]
    private void Rotate180() => ApplyTransform(TransformService.Rotate180);

    [RelayCommand]
    private void RotateByAngle() => ApplyTransform(m => TransformService.Rotate(m, Angle));

    [RelayCommand]
    private void Resize() => ApplyTransform(m => TransformService.Resize(m, ScalePercent / 100.0));

    [RelayCommand]
    private void ApplySkew() => ApplyTransform(m => TransformService.Skew(m, SkewX, SkewY));

    [RelayCommand]
    private void ApplyExactResize() => ApplyTransform(m => TransformService.ResizeTo(m, ExactWidth, ExactHeight));

    [RelayCommand]
    private void ApplyCropAspect() => ApplyTransform(m => TransformService.CropToAspect(m, CropAspectRatio));

    [RelayCommand]
    private void TrimBorder() => ApplyTransform(m => TransformService.TrimBorder(m));

    [RelayCommand]
    private void ApplyPadding() => ApplyTransform(m => TransformService.Pad(m, PadLeft, PadTop, PadRight, PadBottom, new Scalar(0, 0, 0, 0)));

    [RelayCommand]
    private void ApplyFit() => ApplyTransform(m => TransformService.ResizeToFit(m, FitWidth, FitHeight));

    [RelayCommand]
    private void ApplyCenterCrop() => ApplyTransform(m => TransformService.CropCenter(m, CenterCropWidth, CenterCropHeight, new Scalar(0, 0, 0, 0)));

    [RelayCommand]
    private void ApplyTile() => ApplyTransform(m => TransformService.Tile(m, TileWidth, TileHeight));

    [RelayCommand]
    private void AutoStraighten() => ApplyTransform(m => TransformService.AutoStraighten(m));

    [RelayCommand]
    private void Reset()
    {
        if (_sourceImage is null) return;
        WorkingBgra?.Dispose();
        WorkingBgra = _sourceImage.FullBgr.ToBgra(_workingAlpha!);
        Angle = 0.0;
        ScalePercent = 100.0;
        SkewX = 0.0;
        SkewY = 0.0;
        ExactWidth = WorkingBgra.Width;
        ExactHeight = WorkingBgra.Height;
        CropAspectRatio = 1.0;
        PadLeft = 0;
        PadTop = 0;
        PadRight = 0;
        PadBottom = 0;
        CenterCropWidth = WorkingBgra.Width;
        CenterCropHeight = WorkingBgra.Height;
        TileWidth = WorkingBgra.Width;
        TileHeight = WorkingBgra.Height;
        RefreshPreview();
    }

    private void ApplyTransform(Func<Mat, Mat> transform)
    {
        if (WorkingBgra is null) return;
        using var transformed = transform(WorkingBgra);
        WorkingBgra.Dispose();
        WorkingBgra = transformed.Clone();
        RefreshPreview();
    }

    public override Task ApplyAsync() => ApplyWorkingBgraAsync("Transform");
}
