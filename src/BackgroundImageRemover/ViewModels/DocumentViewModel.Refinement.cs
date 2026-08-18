using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
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

    private bool CanUndoExecute() => IsScribbling ? _scribbleManager.CanUndo : _editHistory.CanUndo;
    private bool CanRedoExecute() => IsScribbling ? _scribbleManager.CanRedo : _editHistory.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (IsScribbling && TryUndoScribble())
        {
            ScribbleStrokeUndone?.Invoke(this, EventArgs.Empty);
            RefreshUndoRedoState();
            return;
        }

        if (_workingAlpha is null)
        {
            return;
        }
        var restored = _editHistory.Undo(_workingAlpha);
        if (restored is null)
        {
            return;
        }
        _workingAlpha.Dispose();
        _workingAlpha = restored;
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

        if (_workingAlpha is null)
        {
            return;
        }
        var restored = _editHistory.Redo(_workingAlpha);
        if (restored is null)
        {
            return;
        }
        _workingAlpha.Dispose();
        _workingAlpha = restored;
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

    public void OnResultStrokeStart(WpfPoint imagePoint)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        _editHistory.Push(_workingAlpha);
        _workingResultHandEdited = true;
        IsDirty = true;
        RefreshUndoRedoState();
        _brushLastPoint = imagePoint;
        StampBrush(imagePoint, imagePoint);
    }

    public void OnResultStrokeMove(WpfPoint imagePoint)
    {
        if (_workingAlpha is null || _brushLastPoint is not { } last)
        {
            return;
        }
        StampBrush(last, imagePoint);
        _brushLastPoint = imagePoint;
    }

    public void OnResultStrokeEnd() => _brushLastPoint = null;

    private void StampBrush(WpfPoint from, WpfPoint to)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        BrushEditor.StampSegment(_workingAlpha,
            new Point2f((float)from.X, (float)from.Y), new Point2f((float)to.X, (float)to.Y),
            BrushRadius, BrushHardness, BrushMode);
        RefreshResultBitmapFromWorking();
    }

    public void OnResultWandClicked(Point imagePoint)
    {
        if (_workingAlpha is null || _workingBgr is null)
        {
            return;
        }
        _editHistory.Push(_workingAlpha);
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

        var scribbleMode = OriginalMode == InteractionMode.ScribbleForeground
            ? ScribbleMode.Foreground
            : OriginalMode == InteractionMode.ScribbleBackground
                ? ScribbleMode.Background
                : ScribbleMode.Foreground; // fallback

        ScribbleManager.StartStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        var scribbleMode = OriginalMode == InteractionMode.ScribbleForeground
            ? ScribbleMode.Foreground
            : OriginalMode == InteractionMode.ScribbleBackground
                ? ScribbleMode.Background
                : ScribbleMode.Foreground; // fallback

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
