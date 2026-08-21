using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for selective and whole-image sharpening.</summary>
public partial class SharpenToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly BrushStrokeController _strokes = new();
    private Mat? _paintedMask;

    public override string ToolBadge => "🔪 Sharpen";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private double _brushRadius = 40;

    [ObservableProperty]
    private double _strength = 0.5;

    [ObservableProperty]
    private bool _wholeImage;

    [ObservableProperty]
    private bool _paintMode;

    [ObservableProperty]
    private string? _statusMessage;

    public SharpenToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _paintedMask = new Mat(_sourceImage!.FullBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha!);
        RefreshResult();
        StatusMessage = "Choose whole-image or paint a region to sharpen, then apply.";
    }

    partial void OnBrushRadiusChanged(double value) => RefreshResult();
    partial void OnStrengthChanged(double value) => RefreshResult();
    partial void OnWholeImageChanged(bool value) => RefreshResult();
    partial void OnPaintModeChanged(bool value) => RefreshResult();

    public void OnBrushStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Begin(imagePoint, pixelRadius, StampMask);

    public void OnBrushStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Extend(imagePoint, pixelRadius, StampMask);

    public void OnBrushStrokeEnd()
    {
        _strokes.End();
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
        Strength = 0.5;
        WholeImage = false;
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
            result = SharpenService.SharpenAll(_sourceImage.FullBgr, Strength);
        }
        else if (PaintMode && _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0)
        {
            result = SharpenService.SharpenRegion(_sourceImage.FullBgr, _paintedMask, Strength);
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
                result = SharpenService.SharpenAll(_sourceImage.FullBgr, Strength);
            }
            else if (PaintMode && _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0)
            {
                result = SharpenService.SharpenRegion(_sourceImage.FullBgr, _paintedMask, Strength);
            }
            else
            {
                result = _sourceImage.FullBgr.Clone();
            }

            _parentDocument.ApplyToolResult(result, _workingAlpha!.Clone(), "Sharpen");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
        _paintedMask?.Dispose();
    }
}
