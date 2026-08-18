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

    /// <summary>Raised after a scribble stroke is undone/redone, so the View can keep its stroke visuals in sync.</summary>
    public event EventHandler? ScribbleStrokeUndone;
    public event EventHandler? ScribbleStrokeRedone;

    /// <summary>Raised when scribbles are reset (new image, new rect), so the View can clear stroke visuals.</summary>
    public event EventHandler? ScribblesCleared;

    // --- Undo / Redo: while scribbling, undoes the last scribble stroke; otherwise undoes
    // the last brush/magic-wand edit on the working alpha channel. ---

    private bool IsScribbling => OriginalMode is InteractionMode.ScribbleForeground or InteractionMode.ScribbleBackground;

    private bool CanUndoExecute() => IsScribbling ? _scribbleManager.CanUndo : _editSession.CanUndo;
    private bool CanRedoExecute() => IsScribbling ? _scribbleManager.CanRedo : _editSession.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (IsScribbling && TryUndoScribble())
        {
            ScribbleStrokeUndone?.Invoke(this, EventArgs.Empty);
            RefreshUndoRedoState();
            return;
        }

        if (!_editSession.Undo(ref _workingAlpha))
        {
            return;
        }
        _workingResultHandEdited = true;
        IsDirty = true;
        RefreshUndoRedoState();
        RefreshResultBitmapFromWorking();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (IsScribbling && TryRedoScribble())
        {
            ScribbleStrokeRedone?.Invoke(this, EventArgs.Empty);
            RefreshUndoRedoState();
            return;
        }

        if (!_editSession.Redo(ref _workingAlpha))
        {
            return;
        }
        _workingResultHandEdited = true;
        IsDirty = true;
        RefreshUndoRedoState();
        RefreshResultBitmapFromWorking();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // --- Result-pane refinement: brush and magic wand, operating on the working alpha ---

    [RelayCommand]
    private void SetResultMode(InteractionMode mode) => ResultMode = ResultMode == mode ? InteractionMode.None : mode;

    public void OnResultStrokeStart(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        _editSession.Record(_workingAlpha);
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
        _editSession.Record(_workingAlpha);
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

        ScribbleManager.StartStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        var scribbleMode = ScribbleManager.FromInteractionMode(OriginalMode);

        ScribbleManager.MoveStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeEnd()
    {
        ScribbleManager.EndStroke();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
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
