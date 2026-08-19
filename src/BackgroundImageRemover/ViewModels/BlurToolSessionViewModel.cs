using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for selective and whole-image blur.</summary>
public partial class BlurToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;
    private Mat? _paintedMask;
    private WpfPoint? _brushLastPoint;

    public override string ToolBadge => "🌫 Blur";
    public override string AccentColor => "#0E7490";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _brushRadius = 40;

    [ObservableProperty]
    private double _blurRadius = 12;

    [ObservableProperty]
    private bool _wholeImage;

    [ObservableProperty]
    private bool _motionBlur;

    [ObservableProperty]
    private double _motionAngle;

    [ObservableProperty]
    private bool _paintMode;

    [ObservableProperty]
    private string? _statusMessage;

    public BlurToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _paintedMask = new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
        RefreshResult();
        StatusMessage = "Choose whole-image or paint a region to blur, then apply.";
    }

    partial void OnBrushRadiusChanged(double value) => RefreshResult();
    partial void OnBlurRadiusChanged(double value) => RefreshResult();
    partial void OnWholeImageChanged(bool value) => RefreshResult();
    partial void OnMotionBlurChanged(bool value) => RefreshResult();
    partial void OnMotionAngleChanged(double value) => RefreshResult();
    partial void OnPaintModeChanged(bool value) => RefreshResult();

    public void OnBrushStrokeStart(WpfPoint imagePoint, double pixelRadius)
    {
        _brushLastPoint = imagePoint;
        StampMask(imagePoint, imagePoint, pixelRadius);
    }

    public void OnBrushStrokeMove(WpfPoint imagePoint, double pixelRadius)
    {
        if (_brushLastPoint is { } last)
        {
            StampMask(last, imagePoint, pixelRadius);
        }
        _brushLastPoint = imagePoint;
    }

    public void OnBrushStrokeEnd()
    {
        _brushLastPoint = null;
        RefreshResult();
    }

    private void StampMask(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_paintedMask is null) return;
        MaskBrushHelper.StampSegment(_paintedMask, from, to, pixelRadius);
    }

    [RelayCommand]
    private void ClearMask()
    {
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        BrushRadius = 40;
        BlurRadius = 12;
        WholeImage = false;
        MotionBlur = false;
        MotionAngle = 0;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        Mat result;
        if (WholeImage)
        {
            result = MotionBlur
                ? BlurService.MotionBlur(_sourceImage.FullBgr, BlurRadius, MotionAngle)
                : BlurService.BlurAll(_sourceImage.FullBgr, BlurRadius);
        }
        else if (PaintMode && _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0)
        {
            result = BlurService.BlurRegion(_sourceImage.FullBgr, _paintedMask, BlurRadius);
        }
        else
        {
            result = _sourceImage.FullBgr.Clone();
        }

        using var _ = result;
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = WholeImage || (PaintMode && _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0);
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            Mat result;
            if (WholeImage)
            {
                result = MotionBlur
                    ? BlurService.MotionBlur(_sourceImage.FullBgr, BlurRadius, MotionAngle)
                    : BlurService.BlurAll(_sourceImage.FullBgr, BlurRadius);
            }
            else if (PaintMode && _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0)
            {
                result = BlurService.BlurRegion(_sourceImage.FullBgr, _paintedMask, BlurRadius);
            }
            else
            {
                result = _sourceImage.FullBgr.Clone();
            }

            _parentDocument.ApplyToolResult(result, _workingAlpha.Clone(), "Blur");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
        _paintedMask?.Dispose();
    }
}
