using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    // The busy half of the rotation guard is applied structurally by the gate (rotating
    // disposes the live working Mats, which must never race a background run); this
    // predicate only answers \"is there an image to rotate\".
    private IRelayCommand? _rotate90CwCommand;
    public IRelayCommand Rotate90CwCommand => _rotate90CwCommand ??= _busyGate.Gate(new RelayCommand(() => RotateDocument(clockwise: true), () => IsImageLoaded));

    private IRelayCommand? _rotate90CcwCommand;
    public IRelayCommand Rotate90CcwCommand => _rotate90CcwCommand ??= _busyGate.Gate(new RelayCommand(() => RotateDocument(clockwise: false), () => IsImageLoaded));

    /// <summary>
    /// Rotates the whole document 90° in place — the working result, the source image and the
    /// preview — and records the previous state in the Undo history, so the toolbar quick-rotate
    /// actions behave like a normal edit. The source is rotated too, so strategy previews and
    /// exports afterwards see the new orientation instead of a stale unrotated copy.
    /// </summary>
    public void RotateDocument(bool clockwise)
    {
        if (_loadedImage is null)
        {
            return;
        }

        RecordCurrentStateForUndo(clockwise ? "Rotate 90° CW" : "Rotate 90° CCW");
        bool hadWorkingResult = _workingBgr is not null && _workingAlpha is not null;

        // Rotate the working result (color + alpha) when one exists. A hand-edited result stays
        // authoritative, exactly like the other tools.
        if (hadWorkingResult)
        {
            var newBgr = RotateQuarterTurns(_workingBgr!, clockwise);
            var newAlpha = RotateQuarterTurns(_workingAlpha!, clockwise);
            _workingBgr.Dispose();
            _workingAlpha.Dispose();
            _workingBgr = newBgr;
            _workingAlpha = newAlpha;
            _workingResultIsLoadedCutout = false;
            _workingResultHandEdited = true;
            IsCutout = BackgroundCompositingService.HasMeaningfulTransparency(_workingAlpha);
            RefreshResultBitmapFromWorking();
        }

        // Rotate the source and rebuild the preview so the next strategy run matches the
        // orientation the user just set.
        var filePath = _loadedImage.FilePath;
        var rotatedBgr = RotateQuarterTurns(_loadedImage.FullBgr, clockwise);
        var rotatedAlpha = _loadedImage.FullAlpha is { } alpha ? RotateQuarterTurns(alpha, clockwise) : null;
        _loadedImage.Dispose();
        _loadedImage = new LoadedImage(filePath, rotatedBgr, rotatedAlpha);

        _preview?.Dispose();
        _preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
        PreviewBitmap = _loadedImage.FullAlpha is { } fullAlpha
            ? _preview.Bgr.BuildPreviewWithAlpha(fullAlpha)
            : _preview.Bgr.ToBitmapSource();

        ImageWidth = _loadedImage.FullBgr.Width;
        ImageHeight = _loadedImage.FullBgr.Height;
        OnPropertyChanged(nameof(ImageDimensions));

        // A freshly opened image has no working result yet: adopt the rotated source as the
        // new working state so the rotation is undoable (the undo stack only restores the
        // working BGR/alpha pair). It is intentionally NOT marked hand-edited, so a later
        // strategy run/export still replaces it with the actual removal result.
        if (!hadWorkingResult)
        {
            _workingBgr?.Dispose();
            _workingAlpha?.Dispose();
            _workingBgr = _loadedImage.FullBgr.Clone();
            _workingAlpha = _loadedImage.FullAlpha?.Clone()
                ?? new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
            _workingResultIsLoadedCutout = false;
            _workingResultHandEdited = false;
            IsCutout = BackgroundCompositingService.HasMeaningfulTransparency(_workingAlpha);
            RefreshResultBitmapFromWorking();
            OnPropertyChanged(nameof(HasWorkingResult));
            ExportCommand.NotifyCanExecuteChanged();
        }

        // Scribbles, the SAM prompt point and the magic-wand seed live in the old coordinate
        // space: drop them (and the stale embedding) so the user re-picks in the new
        // orientation instead of painting on the wrong spot.
        _samEmbedding = null;
        _samPromptPointPreview = null;
        Sam.HasClickedPoint = false;
        _magicWandSeedPreview = null;
        MagicWand.HasClickedPoint = false;
        ScribbleManager.Clear();
        GrabCut.HasScribbles = false;
        GrabCut.SelectedRect = null;

        IsDirty = true;
        RefreshUndoRedoState();
        OnPropertyChanged(nameof(DisplayBitmap));
        StatusMessage = $"Rotated {(clockwise ? "90° clockwise" : "90° counter-clockwise")} ({_loadedImage.FullBgr.Width}×{_loadedImage.FullBgr.Height}).";
    }

    private static Mat RotateQuarterTurns(Mat mat, bool clockwise)
        => clockwise ? TransformService.Rotate90Clockwise(mat) : TransformService.Rotate90CounterClockwise(mat);
}
