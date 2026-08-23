using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class UncropViewModel
{
    /// <summary>Seeds the window with an image handed in from the main window (a clone, so this
    /// window's own lifecycle/EditHistory never touches the source document's Mats).</summary>
    public void LoadInitialImage(LoadedImage image) => AdoptImage(image);

    public async Task LoadAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading image...";
            var image = await _imageLoader.LoadAsync(path);
            Title = Path.GetFileName(path) + " (Uncrop)";
            AdoptImage(image);
            StatusMessage = $"Loaded {Path.GetFileName(path)} ({image.FullBgr.Width}x{image.FullBgr.Height})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            _log.Error("Uncrop: could not load image", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Replacing the image while a fill is in flight would dispose the source Mats
    /// the worker may still touch; the gate keeps Open disabled then.</summary>
    private IAsyncRelayCommand? _openImageCommand;
    public IAsyncRelayCommand OpenImageCommand => _openImageCommand ??= _busyGate.Gate(new AsyncRelayCommand(OpenImageAsync));

    private async Task OpenImageAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        await LoadAsync(path);
    }

    private void AdoptImage(LoadedImage image)
    {
        _sourceImage?.Dispose();
        _resultSession.Clear();
        IsDirty = false;
        RefreshUndoRedoState();
        SaveAsCommand.NotifyCanExecuteChanged();

        _sourceImage = image;
        if (string.IsNullOrEmpty(Title) || Title == "Uncrop")
        {
            Title = !string.IsNullOrEmpty(image.FilePath) ? Path.GetFileName(image.FilePath) + " (Uncrop)" : "Uncrop";
        }
        SourceBitmap = image.FullBgr.ToFrozenBitmapSource();
        PreviewResult = null;
        IsImageLoaded = true;
        Options.Reset();
    }

    // The busy half of the fill guard comes from the gate (ApplyFillCommand is routed through
    // it below); this predicate only answers "is there a valid configuration".
    private bool CanApplyFill() => IsImageLoaded && Options.CanExecute();

    private bool CanCancelFill() => IsBusy && _fillCts is not null && !_fillCts.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanCancelFill))]
    private void CancelFill()
    {
        if (_fillCts is not null && !_fillCts.IsCancellationRequested)
        {
            _fillCts.Cancel();
            StatusMessage = "Cancelling fill operation...";
        }
    }

    private IAsyncRelayCommand? _applyFillCommand;
    public IAsyncRelayCommand ApplyFillCommand => _applyFillCommand ??= _busyGate.Gate(new AsyncRelayCommand(ApplyFillAsync, CanApplyFill));

    private async Task ApplyFillAsync()
    {
        if (_sourceImage is null)
        {
            return;
        }

        var config = Options.ToConfig();

        _fillCts?.Dispose();
        _fillCts = new CancellationTokenSource();
        var ct = _fillCts.Token;

        try
        {
            IsBusy = true;
            CancelFillCommand.NotifyCanExecuteChanged();
            StatusMessage = config.FillMode == UncropFillMode.AiOutpaint
                ? "Preparing AI outpainting model (first use downloads ~200 MB)..."
                : "Filling...";

            // Snapshot the source on the UI thread: the fill runs on a worker and closing the
            // window mid-run disposes _sourceImage, which would otherwise be read after disposal.
            using var sourceBgr = _sourceImage.FullBgr.Clone();
            using var filledBgr = await UncropOperationHelper.ExecuteUncropAsync(
                sourceBgr, config, _fillService, _aiOutpaintService, DownloadProgress(), ct);

            var bgra = UncropOperationHelper.ApplyFinishing(filledBgr, config);

            _resultSession.Replace(bgra);

            RefreshUndoRedoState();
            SaveAsCommand.NotifyCanExecuteChanged();
            PreviewResult = _resultSession.Result!.ToFrozenBitmapSource();
            IsDirty = true;
            StatusMessage = $"Applied {config.FillMode} fill.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Fill operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fill failed: {ex.Message}";
            _log.Error("Uncrop: fill failed", ex);
        }
        finally
        {
            _fillCts?.Dispose();
            _fillCts = null;
            IsBusy = false;
            CancelFillCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Reports the first-use model download through the status bar (AI fill only).</summary>
    private IProgress<ModelDownloadProgress>? DownloadProgress()
        => Options.SelectedFillMode == UncropFillMode.AiOutpaint
            ? new Progress<ModelDownloadProgress>(p => StatusMessage = p.FractionComplete is { } f
                ? $"Downloading AI outpainting model... {f:P0}"
                : "Downloading AI outpainting model...")
            : null;

    private bool CanUndoExecute() => _resultSession.CanUndo;
    private bool CanRedoExecute() => _resultSession.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (!_resultSession.Undo())
        {
            return;
        }
        PreviewResult = _resultSession.Result!.ToFrozenBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (!_resultSession.Redo())
        {
            return;
        }
        PreviewResult = _resultSession.Result!.ToFrozenBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => _resultSession.HasResult;

    private IAsyncRelayCommand? _saveAsCommand;
    public IAsyncRelayCommand SaveAsCommand => _saveAsCommand ??= _busyGate.Gate(new AsyncRelayCommand(SaveAsAsync, CanSave));

    private async Task SaveAsAsync()
    {
        await _resultSession.SaveAsync();
    }

    public void Dispose()
    {
        _fillCts?.Cancel();
        _fillCts?.Dispose();
        _sourceImage?.Dispose();
        _resultSession.Dispose();
    }
}
