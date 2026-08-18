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
    private LoadedImage? _sourceImage;
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
    private string? _statusMessage;

    public TransformToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        using var alpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _workingBgra = _sourceImage.FullBgr.ToBgra(alpha);
        ExactWidth = _workingBgra.Width;
        ExactHeight = _workingBgra.Height;
        RefreshPreview();
        StatusMessage = "Apply flips, rotations, scaling, skew, crop and trim.";
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
    private void Reset()
    {
        if (_sourceImage is null) return;
        _workingBgra?.Dispose();
        using var alpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _workingBgra = _sourceImage.FullBgr.ToBgra(alpha);
        Angle = 0.0;
        ScalePercent = 100.0;
        SkewX = 0.0;
        SkewY = 0.0;
        ExactWidth = _workingBgra.Width;
        ExactHeight = _workingBgra.Height;
        CropAspectRatio = 1.0;
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
        _sourceImage?.Dispose();
        _workingBgra?.Dispose();
    }
}
