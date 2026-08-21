using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for Brush and Magic Wand retouching on alpha / pixels.
/// </summary>
public partial class RetouchToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly MatEditSession _editSession = new();
    private readonly DispatcherTimer _brushRefreshTimer;
    private readonly BrushStrokeController _strokes = new();

    private Mat? _workingBgr;

    public override string ToolBadge => "🖌 Retouch";
    public override string AccentColor => "#8E24AA";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private InteractionMode _resultMode = InteractionMode.Brush;

    [ObservableProperty]
    private BrushMode _brushMode = BrushMode.Erase;

    [ObservableProperty]
    private double _brushRadius = 24.0;

    [ObservableProperty]
    private double _brushHardness = 0.5;

    [ObservableProperty]
    private double _brushOpacity = 1.0;

    [ObservableProperty]
    private double _magicWandTolerance = 25.0;

    [ObservableProperty]
    private double _dehaze;

    [ObservableProperty]
    private bool _defringe;

    [ObservableProperty]
    private int _blurBackgroundRadius;

    [ObservableProperty]
    private double _sharpenStrength;

    [ObservableProperty]
    private double _colorBoost;

    [ObservableProperty]
    private int _removeDustKernel;

    [ObservableProperty]
    private double _surfaceBlur;

    [ObservableProperty]
    private bool _autoContrast;

    [ObservableProperty]
    private bool _autoWhiteBalance;

    [ObservableProperty]
    private double _chromaticAberration;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string? _statusMessage;

    public RetouchToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        _brushRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _brushRefreshTimer.Tick += (_, _) =>
        {
            _brushRefreshTimer.Stop();
            RefreshResultBitmap();
        };

        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgr = _sourceImage!.FullBgr.Clone();
        RefreshResultBitmap();
        StatusMessage = "Use Brush or Magic Wand to refine foreground & edges.";
    }

    [RelayCommand]
    private void SetResultMode(InteractionMode mode) => ResultMode = ResultMode == mode ? InteractionMode.None : mode;

    public void OnResultStrokeStart(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null) return;
        _editSession.Record(_workingAlpha);
        IsDirty = true;
        RefreshUndoRedoState();
        _strokes.Begin(imagePoint, pixelRadius, StampBrush);
    }

    public void OnResultStrokeMove(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null) return;
        _strokes.Extend(imagePoint, pixelRadius, StampBrush);
    }

    public void OnResultStrokeEnd()
    {
        _brushRefreshTimer.Stop();
        RefreshResultBitmap();
        _strokes.End();
    }

    private void StampBrush(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_workingAlpha is null) return;
        BrushEditor.StampSegment(_workingAlpha,
            new Point2f((float)from.X, (float)from.Y), new Point2f((float)to.X, (float)to.Y),
            pixelRadius, BrushHardness, BrushMode, BrushOpacity);
        RequestBrushRefresh();
    }

    /// <summary>Recomposites the result bitmap at most every <c>_brushRefreshTimer</c> interval
    /// while painting, so long brush strokes stay responsive instead of rebuilding a full-size
    /// bitmap on every mouse-move event.</summary>
    private void RequestBrushRefresh()
    {
        if (_brushRefreshTimer.IsEnabled)
        {
            return;
        }

        RefreshResultBitmap();
        _brushRefreshTimer.Start();
    }

    public void OnResultWandClicked(Point imagePoint)
    {
        if (_workingAlpha is null || _workingBgr is null) return;
        _editSession.Record(_workingAlpha);
        IsDirty = true;
        RefreshUndoRedoState();
        MagicWandService.Apply(_workingBgr, _workingAlpha, imagePoint, MagicWandTolerance, add: BrushMode == BrushMode.Restore);
        RefreshResultBitmap();
    }

    partial void OnDehazeChanged(double value) => RefreshResultBitmap();
    partial void OnDefringeChanged(bool value) => RefreshResultBitmap();
    partial void OnBlurBackgroundRadiusChanged(int value) => RefreshResultBitmap();
    partial void OnSharpenStrengthChanged(double value) => RefreshResultBitmap();
    partial void OnColorBoostChanged(double value) => RefreshResultBitmap();
    partial void OnRemoveDustKernelChanged(int value) => RefreshResultBitmap();
    partial void OnSurfaceBlurChanged(double value) => RefreshResultBitmap();
    partial void OnAutoContrastChanged(bool value) => RefreshResultBitmap();
    partial void OnAutoWhiteBalanceChanged(bool value) => RefreshResultBitmap();
    partial void OnChromaticAberrationChanged(double value) => RefreshResultBitmap();

    /// <summary>Applies the whole-image retouch effects on top of the brush/wand alpha edits.</summary>
    private Mat BuildResultBgr()
    {
        var result = _workingBgr!.Clone();
        try
        {
            if (RemoveDustKernel > 0)
            {
                var dusted = RetouchEffectsService.RemoveDust(result, RemoveDustKernel);
                result.Dispose();
                result = dusted;
            }
            if (SurfaceBlur > 1e-4)
            {
                var smoothed = RetouchEffectsService.SurfaceBlur(result, SurfaceBlur);
                result.Dispose();
                result = smoothed;
            }
            if (AutoContrast)
            {
                var contrasted = RetouchEffectsService.AutoContrast(result);
                result.Dispose();
                result = contrasted;
            }
            if (AutoWhiteBalance)
            {
                var balanced = RetouchEffectsService.AutoWhiteBalance(result);
                result.Dispose();
                result = balanced;
            }
            if (ChromaticAberration > 1e-4)
            {
                var aberrated = RetouchEffectsService.ChromaticAberration(result, ChromaticAberration);
                result.Dispose();
                result = aberrated;
            }
            if (Dehaze > 1e-4)
            {
                var dehazed = RetouchEffectsService.Dehaze(result, Dehaze);
                result.Dispose();
                result = dehazed;
            }
            if (BlurBackgroundRadius > 0)
            {
                var blurred = RetouchEffectsService.BlurBackground(result, _workingAlpha!, BlurBackgroundRadius);
                result.Dispose();
                result = blurred;
            }
            if (SharpenStrength > 1e-4)
            {
                var sharpened = RetouchEffectsService.SharpenSubject(result, _workingAlpha!, SharpenStrength);
                result.Dispose();
                result = sharpened;
            }
            if (ColorBoost > 1e-4)
            {
                var boosted = RetouchEffectsService.ColorBoost(result, _workingAlpha!, ColorBoost);
                result.Dispose();
                result = boosted;
            }
            if (Defringe)
            {
                var defringed = RetouchEffectsService.Defringe(result, _workingAlpha!);
                result.Dispose();
                result = defringed;
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private void RefreshResultBitmap()
    {
        if (_workingBgr is null || _workingAlpha is null) return;
        using var result = BuildResultBgr();
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
    }

    private void RefineAlpha(Func<Mat, Mat> refine)
    {
        if (_workingAlpha is null) return;
        _editSession.Record(_workingAlpha);
        var refined = refine(_workingAlpha);
        _workingAlpha.Dispose();
        _workingAlpha = refined;
        IsDirty = true;
        RefreshUndoRedoState();
        RefreshResultBitmap();
    }

    [RelayCommand]
    private void SmoothEdges() => RefineAlpha(a => AlphaRefinementService.Smooth(a));

    [RelayCommand]
    private void FeatherEdges() => RefineAlpha(a => AlphaRefinementService.Feather(a, 2.0));

    [RelayCommand]
    private void RemoveSpecks() => RefineAlpha(a => AlphaRefinementService.RemoveSpecks(a));

    [RelayCommand]
    private void InvertMask() => RefineAlpha(a => AlphaRefinementService.Invert(a));

    private bool CanUndoExecute() => _editSession.CanUndo;
    private bool CanRedoExecute() => _editSession.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (!_editSession.Undo(ref _workingAlpha)) return;
        IsDirty = true;
        RefreshUndoRedoState();
        RefreshResultBitmap();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (!_editSession.Redo(ref _workingAlpha)) return;
        IsDirty = true;
        RefreshUndoRedoState();
        RefreshResultBitmap();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public override Task ApplyAsync()
    {
        if (_workingBgr is not null && _workingAlpha is not null)
        {
            using var resultBgr = BuildResultBgr();
            _parentDocument.ApplyToolResult(resultBgr.Clone(), _workingAlpha.Clone(), "Retouch & Brush");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _brushRefreshTimer.Stop();
        _workingBgr?.Dispose();
        _editSession.Dispose();
        base.Dispose();
    }
}
