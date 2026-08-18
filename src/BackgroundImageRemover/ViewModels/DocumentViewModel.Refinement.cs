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

    private bool CanUndoExecute() => IsScribbling ? _scribbleUndo.Count > 0 : _editHistory.CanUndo;
    private bool CanRedoExecute() => IsScribbling ? _scribbleRedo.Count > 0 : _editHistory.CanRedo;

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
        EnsureScribbleMats();
        PushScribbleUndoSnapshot();
        _scribbleLastPoint = imagePoint;
        DrawScribbleSegment(imagePoint, imagePoint);
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        if (_scribbleLastPoint is not { } last)
        {
            return;
        }
        DrawScribbleSegment(last, imagePoint);
        _scribbleLastPoint = imagePoint;
    }

    public void OnOriginalStrokeEnd() => _scribbleLastPoint = null;

    private void DrawScribbleSegment(WpfPoint from, WpfPoint to)
    {
        var target = OriginalMode == InteractionMode.ScribbleForeground ? _grabCutFgScribble
            : OriginalMode == InteractionMode.ScribbleBackground ? _grabCutBgScribble
            : null;
        if (target is null)
        {
            return;
        }
        Cv2.Line(target, new Point((int)from.X, (int)from.Y), new Point((int)to.X, (int)to.Y), Scalar.All(255), thickness: 6);
        GrabCut.HasScribbles = HasNonEmptyScribbles();
    }

    private void EnsureScribbleMats()
    {
        if (_preview is null)
        {
            return;
        }
        _grabCutFgScribble ??= new Mat(_preview.Bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        _grabCutBgScribble ??= new Mat(_preview.Bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
    }

    private bool HasNonEmptyScribbles()
        => (_grabCutFgScribble is not null && Cv2.CountNonZero(_grabCutFgScribble) > 0)
        || (_grabCutBgScribble is not null && Cv2.CountNonZero(_grabCutBgScribble) > 0);

    private void ClearScribbles()
    {
        _grabCutFgScribble?.Dispose();
        _grabCutBgScribble?.Dispose();
        _grabCutFgScribble = null;
        _grabCutBgScribble = null;
        GrabCut.HasScribbles = false;

        foreach (var (fg, bg) in _scribbleUndo) { fg.Dispose(); bg.Dispose(); }
        foreach (var (fg, bg) in _scribbleRedo) { fg.Dispose(); bg.Dispose(); }
        _scribbleUndo.Clear();
        _scribbleRedo.Clear();
        RefreshUndoRedoState();
        ScribblesCleared?.Invoke(this, EventArgs.Empty);
    }

    private const int MaxScribbleHistoryDepth = 20;

    private void PushScribbleUndoSnapshot()
    {
        if (_grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return;
        }

        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        _scribbleUndo.TrimStack(MaxScribbleHistoryDepth, drop =>
        {
            drop.Fg.Dispose();
            drop.Bg.Dispose();
        });

        foreach (var (fg, bg) in _scribbleRedo) { fg.Dispose(); bg.Dispose(); }
        _scribbleRedo.Clear();
        RefreshUndoRedoState();
    }

    private bool TryUndoScribble()
    {
        if (_scribbleUndo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return false;
        }
        _scribbleRedo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleUndo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        return true;
    }

    private bool TryRedoScribble()
    {
        if (_scribbleRedo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return false;
        }
        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleRedo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        return true;
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !HasNonEmptyScribbles())
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

    private static Mat? ResizeScribbleToSize(Mat? scribble, Size targetSize)
    {
        if (scribble is null)
        {
            return null;
        }
        var resized = new Mat();
        Cv2.Resize(scribble, resized, targetSize, interpolation: InterpolationFlags.Nearest);
        return resized;
    }
}
