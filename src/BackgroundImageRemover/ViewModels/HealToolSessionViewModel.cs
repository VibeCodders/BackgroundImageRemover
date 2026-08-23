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
public partial class HealToolSessionViewModel : WorkingCopyToolSessionViewModelBase, ITool, IBrushStrokeSession
{
    private readonly BrushStrokeController _strokes = new();
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
    [ToolParameter]
    private double _healRadius = 3;

    [ObservableProperty]
    [ToolParameter]
    private InpaintTypes _inpaintMethod = InpaintTypes.Telea;

    [ObservableProperty]
    [ToolParameter]
    private int _removeDustKernel;

    [ObservableProperty]
    [ToolParameter]
    private double _removeScratchesStrength;

    [ObservableProperty]
    [ToolParameter]
    private double _surfaceSmoothStrength;

    [ObservableProperty]
    [ToolParameter]
    private double _detailEnhanceStrength;

    public HealToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _workingBgr = CloneSourceWorkingBgr();
        _healMask = new Mat(_workingBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _workingBgr.ToBitmapSource(_workingAlpha!);
        RefreshResult();
        StatusMessage = "Paint over blemishes, then apply the heal.";
    }

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

    protected override Mat BuildResult()
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

    public override Task ApplyAsync()
    {
        ApplyAndClose(_workingBgr is not null && _workingAlpha is not null ? BuildResult() : null, "Heal");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _healMask?.Dispose();
        base.Dispose();
    }
}
