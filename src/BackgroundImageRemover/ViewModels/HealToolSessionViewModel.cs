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
public partial class HealToolSessionViewModel : ToolSessionViewModelBase, ITool, IBrushStrokeSession
{
    private readonly BrushStrokeController _strokes = new();
    private Mat? _workingBgr;
    private Mat? _healMask;

    public override string ToolBadge => "🩹 Heal";
    public override string AccentColor => "#DC2626";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

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

    public HealToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgr = CloneWorkingBgr();
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

    public void OnStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Begin(imagePoint, pixelRadius, StampMask);

    public void OnStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => _strokes.Extend(imagePoint, pixelRadius, StampMask);

    public void OnStrokeEnd()
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
        bool owns = true;
        var result = _workingBgr!.Clone();
        result = result.SafeChainWithCatch(r => _healMask is not null && Cv2.CountNonZero(_healMask) > 0 ? HealService.HealRegion(r, _healMask, HealRadius, InpaintMethod) : r, ref owns);
        result = result.SafeChainWithCatch(r => RemoveDustKernel > 0 ? HealService.RemoveDust(r, RemoveDustKernel) : r, ref owns);
        result = result.SafeChainWithCatch(r => RemoveScratchesStrength > 1e-4 ? HealService.RemoveScratches(r, RemoveScratchesStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => SurfaceSmoothStrength > 1e-4 ? HealService.SurfaceSmooth(r, SurfaceSmoothStrength) : r, ref owns);
        result = result.SafeChainWithCatch(r => DetailEnhanceStrength > 1e-4 ? HealService.DetailEnhance(r, DetailEnhanceStrength) : r, ref owns);
        return result;
    }

    private void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;
        using var result = BuildResult();
        ResultBitmap = result.ToBitmapSource(_workingAlpha!);
    }

    public override Task ApplyAsync()
    {
        ApplyAndClose(_workingBgr is not null && _workingAlpha is not null ? BuildResult() : null, "Heal");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgr?.Dispose();
        _healMask?.Dispose();
        base.Dispose();
    }
}
