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

/// <summary>Dedicated Tool Tab for geometric transforms (flip, rotate, resize).</summary>
public partial class TransformToolSessionViewModel : ToolSessionViewModelBase
{
    private Mat? _workingBgra;

    public override string ToolBadge => "↻ Transform";
    public override string AccentColor => "#2563EB";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

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

    [ObservableProperty]
    private string? _statusMessage;

    public TransformToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgra = _sourceImage!.FullBgr.ToBgra(_workingAlpha!);
        ExactWidth = _workingBgra.Width;
        ExactHeight = _workingBgra.Height;
        FitWidth = 1024;
        FitHeight = 1024;
        CenterCropWidth = _workingBgra.Width;
        CenterCropHeight = _workingBgra.Height;
        TileWidth = _workingBgra.Width;
        TileHeight = _workingBgra.Height;
        RefreshPreview();
        StatusMessage = "Apply flips, rotations, scaling, skew, crop, trim, padding, fit, tile and auto-straighten.";
    }

    private void RefreshPreview()
    {
        if (_workingBgra is null) return;
        ResultBitmap = _workingBgra.ToBitmapSource();
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
        _workingBgra?.Dispose();
        _workingBgra = _sourceImage.FullBgr.ToBgra(_workingAlpha!);
        Angle = 0.0;
        ScalePercent = 100.0;
        SkewX = 0.0;
        SkewY = 0.0;
        ExactWidth = _workingBgra.Width;
        ExactHeight = _workingBgra.Height;
        CropAspectRatio = 1.0;
        PadLeft = 0;
        PadTop = 0;
        PadRight = 0;
        PadBottom = 0;
        CenterCropWidth = _workingBgra.Width;
        CenterCropHeight = _workingBgra.Height;
        TileWidth = _workingBgra.Width;
        TileHeight = _workingBgra.Height;
        RefreshPreview();
    }

    private void ApplyTransform(Func<Mat, Mat> transform)
    {
        if (_workingBgra is null) return;
        using var transformed = transform(_workingBgra);
        _workingBgra.Dispose();
        _workingBgra = transformed.Clone();
        RefreshPreview();
    }

    public override Task ApplyAsync()
    {
        if (_workingBgra is not null)
        {
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(_workingBgra);
            _parentDocument.ApplyToolResult(bgr, alpha, "Transform");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgra?.Dispose();
        base.Dispose();
    }
}
