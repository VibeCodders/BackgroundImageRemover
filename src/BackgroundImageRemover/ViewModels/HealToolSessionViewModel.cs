using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for healing blemishes (inpaint brush) and repairing dust/scratches.</summary>
public partial class HealToolSessionViewModel : ToolSessionViewModelBase, ITool
{
    private readonly BrushStrokeController _strokes = new();
    private Mat? _workingBgr;
    private Mat? _healMask;

    public override string ToolBadge => "🩹 Heal";
    public override string AccentColor => "#DC2626";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private InteractionMode _resultMode = InteractionMode.Brush;

    [ObservableProperty]
    private double _brushRadius = 20;

    [ObservableProperty]
    private double _healRadius = 3;

    [ObservableProperty]
    private InpaintMethod _inpaintMethod = InpaintMethod.Telea;

    [ObservableProperty]
    private int _removeDustKernel;

    [ObservableProperty]
    private double _removeScratchesStrength;

    [ObservableProperty]
    private double _surfaceSmoothStrength;

    [ObservableProperty]
    private double _detailEnhanceStrength;

    [ObservableProperty]
    private string? _statusMessage;

    public HealToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgr = _sourceImage!.FullBgr.Clone();
        _healMask = new Mat(_workingBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _workingBgr.ToBitmapSource(_workingAlpha!);
        RefreshResult();
        StatusMessage = "Paint over blemishes, then apply the heal.";
    }

    partial void OnRemoveDustKernelChanged(int value) => RefreshResult();
    partial void OnRemoveScratchesStrengthChanged(double value) => RefreshResult();
    partial void OnSurfaceSmoothStrengthChanged(double value) => RefreshResult();
    partial void OnDetailEnhanceStrengthChanged(double value) => RefreshResult();
    partial void OnHealRadiusChanged(double value) => RefreshResult();
    partial void OnInpaintMethodChanged(InpaintMethod value) => RefreshResult();

    public void OnResultStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Begin(imagePoint, pixelRadius, StampMask);

    public void OnResultStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Extend(imagePoint, pixelRadius, StampMask);

    public void OnResultStrokeEnd()
    {
        _strokes.End();
        IsDirty = Cv2.CountNonZero(_healMask!) > 0;
        RefreshResult();
    }

    private void StampMask(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_healMask is null) return;
        MaskBrushHelper.StampSegment(_healMask, from, to, pixelRadius);
        IsDirty = true;
    }

    [RelayCommand]
    private void ClearMask()
    {
        _healMask?.SetTo(Scalar.All(0));
        IsDirty = false;
        RefreshResult();
    }

    private Mat BuildResult()
    {
        var result = _workingBgr!.Clone();
        try
        {
            if (_healMask is not null && Cv2.CountNonZero(_healMask) > 0)
            {
                var healed = HealService.HealRegion(result, _healMask, HealRadius, InpaintMethod);
                result.Dispose();
                result = healed;
            }
            if (RemoveDustKernel > 0)
            {
                var dusted = HealService.RemoveDust(result, RemoveDustKernel);
                result.Dispose();
                result = dusted;
            }
            if (RemoveScratchesStrength > 1e-4)
            {
                var cleaned = HealService.RemoveScratches(result, RemoveScratchesStrength);
                result.Dispose();
                result = cleaned;
            }
            if (SurfaceSmoothStrength > 1e-4)
            {
                var smoothed = HealService.SurfaceSmooth(result, SurfaceSmoothStrength);
                result.Dispose();
                result = smoothed;
            }
            if (DetailEnhanceStrength > 1e-4)
            {
                var enhanced = HealService.DetailEnhance(result, DetailEnhanceStrength);
                result.Dispose();
                result = enhanced;
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
        if (_workingBgr is null || _workingAlpha is null) return;
        using var result = BuildResult();
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
    }

    public override Task ApplyAsync()
    {
        if (_workingBgr is not null && _workingAlpha is not null)
        {
            var result = BuildResult();
            _parentDocument.ApplyToolResult(result, _workingAlpha.Clone(), "Heal");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgr?.Dispose();
        _healMask?.Dispose();
        base.Dispose();
    }
}
