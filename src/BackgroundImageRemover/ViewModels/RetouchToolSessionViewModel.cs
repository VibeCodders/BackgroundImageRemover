using System.Windows.Media.Imaging;
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
    private LoadedImage? _sourceImage;

    private Mat? _workingBgr;
    private Mat? _workingAlpha;
    private WpfPoint? _brushLastPoint;

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
    private double _magicWandTolerance = 25.0;

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
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingBgr = _sourceImage.FullBgr.Clone();
        _workingAlpha = _sourceImage.FullAlpha?.Clone() ?? new Mat(_workingBgr.Size(), MatType.CV_8UC1, new Scalar(255));
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
        _brushLastPoint = imagePoint;
        StampBrush(imagePoint, imagePoint, pixelRadius);
    }

    public void OnResultStrokeMove(WpfPoint imagePoint, double pixelRadius)
    {
        if (_workingAlpha is null || _brushLastPoint is not { } last) return;
        StampBrush(last, imagePoint, pixelRadius);
        _brushLastPoint = imagePoint;
    }

    public void OnResultStrokeEnd() => _brushLastPoint = null;

    private void StampBrush(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_workingAlpha is null) return;
        BrushEditor.StampSegment(_workingAlpha,
            new Point2f((float)from.X, (float)from.Y), new Point2f((float)to.X, (float)to.Y),
            pixelRadius, BrushHardness, BrushMode);
        RefreshResultBitmap();
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

    private void RefreshResultBitmap()
    {
        if (_workingBgr is null || _workingAlpha is null) return;
        ResultBitmap = _workingBgr.ToBitmapSource(_workingAlpha);
    }

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
            _parentDocument.ApplyToolResult(_workingBgr.Clone(), _workingAlpha.Clone(), "Retouch & Brush");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
        _editSession.Dispose();
    }
}
