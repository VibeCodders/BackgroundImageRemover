using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    [ObservableProperty]
    private double _brushRadius = 20;

    [ObservableProperty]
    private double _brushHardness = 0.5;

    private DispatcherTimer? _brushRefreshTimer;

    [ObservableProperty]
    private BrushMode _brushMode = BrushMode.Restore;

    [ObservableProperty]
    private double _magicWandTolerance = 20;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    // --- Undo / Redo: while scribbling (or erasing scribbles), undoes the last scribble
    // stroke; otherwise undoes the last brush/magic-wand edit on the working alpha channel. ---

    private bool IsScribbling => OriginalMode is InteractionMode.ScribbleForeground
        or InteractionMode.ScribbleBackground
        or InteractionMode.EraseForeground
        or InteractionMode.EraseBackground;

    // While a background run (preview, full-res export, adjustments, uncrop) is in flight,
    // Undo/Redo must stay disabled: the UI thread may dispose the live Mats mid-run and
    // racing a history restore against the worker was surfacing as "Cannot access a disposed
    // object". The run's inputs are snapshots, but the working result it writes back is live.
    private bool CanUndoExecute() => !IsBusy && (IsScribbling ? _scribbleManager.CanUndo : _history.CanUndo);
    private bool CanRedoExecute() => !IsBusy && (IsScribbling ? _scribbleManager.CanRedo : _history.CanRedo);

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (IsScribbling && TryUndoScribble())
        {
            RefreshUndoRedoState();
            return;
        }

        if (_loadedImage is null)
        {
            return;
        }

        if (!_history.Undo(ref _workingBgr, ref _workingAlpha, out var name))
        {
            return;
        }
        FinalizeHistoryRestore($"Undone: {name}");
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (IsScribbling && TryRedoScribble())
        {
            RefreshUndoRedoState();
            return;
        }

        if (_loadedImage is null)
        {
            return;
        }

        if (!_history.Redo(ref _workingBgr, ref _workingAlpha, out var name))
        {
            return;
        }
        FinalizeHistoryRestore($"Redone: {name}");
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Re-evaluates Undo/Redo availability when a background run starts or ends, so
    /// the buttons (and the <see cref="CanUndo"/>/<see cref="CanRedo"/> state) never lag
    /// behind the busy gate.</summary>
    partial void OnIsBusyChanged(bool value) => RefreshUndoRedoState();

    // --- Result-pane refinement: brush and magic wand, operating on the working alpha ---

    [RelayCommand]
    private void SetResultMode(InteractionMode mode) => ResultMode = ResultMode == mode ? InteractionMode.None : mode;

    public void OnResultStrokeStart(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null || _workingBgr is null)
        {
            return;
        }
        _history.Record("Brush stroke", _workingBgr, _workingAlpha);
        _workingResultHandEdited = true;
        IsDirty = true;
        RefreshUndoRedoState();
        _brushLastPoint = imagePoint;
        StampBrush(imagePoint, imagePoint, pixelRadius);
    }

    public void OnResultStrokeMove(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null || _brushLastPoint is not { } last)
        {
            return;
        }
        StampBrush(last, imagePoint, pixelRadius);
        _brushLastPoint = imagePoint;
    }

    public void OnResultStrokeEnd()
    {
        _brushRefreshTimer?.Stop();
        RefreshResultBitmapFromWorking();
        _brushLastPoint = null;
    }

    private void StampBrush(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        BrushEditor.StampSegment(_workingAlpha,
            new Point2f((float)from.X, (float)from.Y), new Point2f((float)to.X, (float)to.Y),
            pixelRadius, BrushHardness, BrushMode);
        RequestBrushRefresh();
    }

    /// <summary>Recomposites the result bitmap at most every 40ms while painting, so long brush
    /// strokes stay responsive instead of rebuilding a full-size bitmap on every mouse-move.</summary>
    private void RequestBrushRefresh()
    {
        if (_brushRefreshTimer is null)
        {
            _brushRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _brushRefreshTimer.Tick += (_, _) =>
            {
                _brushRefreshTimer!.Stop();
                RefreshResultBitmapFromWorking();
            };
        }

        if (_brushRefreshTimer.IsEnabled)
        {
            return;
        }

        RefreshResultBitmapFromWorking();
        _brushRefreshTimer.Start();
    }

    public void OnResultWandClicked(Point imagePoint)
    {
        if (_workingAlpha is null || _workingBgr is null)
        {
            return;
        }
        _history.Record("Magic wand", _workingBgr, _workingAlpha);
        _workingResultHandEdited = true;
        IsDirty = true;
        RefreshUndoRedoState();
        MagicWandService.Apply(_workingBgr, _workingAlpha, imagePoint, MagicWandTolerance, add: BrushMode == BrushMode.Restore);
        RefreshResultBitmapFromWorking();
    }

    // --- Original-pane GrabCut scribble refinement ---

    [RelayCommand]
    private void SetOriginalScribbleMode(InteractionMode mode)
        => OriginalMode = OriginalMode == mode ? InteractionMode.DrawRect : mode;

    public void OnOriginalStrokeStart(WpfPoint imagePoint)
    {
        if (_preview is null) return;
        ScribbleManager.EnsureMats(_preview.Bgr.Size());

        var scribbleMode = ScribbleManager.FromInteractionMode(OriginalMode);
        if (ScribbleManager.IsEraseMode(OriginalMode))
        {
            ScribbleManager.StartErase(imagePoint, scribbleMode);
        }
        else
        {
            ScribbleManager.StartStroke(imagePoint, scribbleMode);
        }

        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        var scribbleMode = ScribbleManager.FromInteractionMode(OriginalMode);
        if (ScribbleManager.IsEraseMode(OriginalMode))
        {
            ScribbleManager.MoveErase(imagePoint, scribbleMode);
        }
        else
        {
            ScribbleManager.MoveStroke(imagePoint, scribbleMode);
        }

        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    public void OnOriginalStrokeEnd()
    {
        ScribbleManager.EndStroke();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        RefreshScribbleOverlay();
    }

    private void RefreshScribbleOverlay()
    {
        ScribbleOverlay = ScribbleManager.BuildOverlayBitmap();
    }

    private bool TryUndoScribble()
    {
        var success = ScribbleManager.Undo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        return success;
    }

    private bool TryRedoScribble()
    {
        var success = ScribbleManager.Redo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        return success;
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !_scribbleManager.HasScribbles)
        {
            StatusMessage = "Add scribbles first.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Refining selection...";
            await RunPreviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refine failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
