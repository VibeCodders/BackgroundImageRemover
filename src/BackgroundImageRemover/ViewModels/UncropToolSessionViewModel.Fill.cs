using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class UncropToolSessionViewModel
{
    private void AdoptImage(LoadedImage image)
    {
        _sourceImage?.Dispose();
        _resultSession.Clear();
        IsDirty = false;
        RefreshUndoRedoState();
        SaveAsCommand.NotifyCanExecuteChanged();

        _sourceImage = image;
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
            // tab mid-run disposes _sourceImage, which would otherwise be read after disposal.
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

    public override async Task ApplyAsync()
    {
        if (IsBusy)
        {
            return;
        }

        // If result is not generated yet, try generating if padding is set
        if (!_resultSession.HasResult && CanApplyFill())
        {
            await ApplyFillAsync();
        }

        if (_resultSession.Result is not null)
        {
            ApplyBgra(_resultSession.Result, "Uncrop Fill");
        }

        _shell.CloseTabDirect(this);
    }

    public override void Dispose()
    {
        _fillCts?.Cancel();
        _fillCts?.Dispose();
        _resultSession.Dispose();
        base.Dispose();
    }
}
