using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    private void RequestPreviewDebounced()
    {
        if (!IsImageLoaded || ResultMode != InteractionMode.None)
        {
            return;
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private StrategyContext BuildContext(double scaleToFull = 1.0, Mat? grabCutFg = null, Mat? grabCutBg = null)
        => StrategyContextBuilder.Build(
            this, scaleToFull, grabCutFg, grabCutBg,
            _samPromptPointPreview, _samPromptPointsPreview, _samEmbedding, _magicWandSeedPreview);

    private void AdoptLoadedCutout()
    {
        if (_loadedImage?.FullAlpha is not { } alpha)
        {
            return;
        }

        DisposeWorkingResult();
        _workingBgr = _loadedImage.FullBgr.Clone();
        _workingAlpha = alpha.Clone();
        _workingResultIsLoadedCutout = true;
        _workingResultHandEdited = false;

        _history.Clear();
        RefreshUndoRedoState();
        OnPropertyChanged(nameof(HasWorkingResult));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        IsDirty = false; // the loaded cutout matches the file on disk until it is edited
        RefreshResultBitmapFromWorking();

        StatusMessage = $"Loaded cutout ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
    }

    private async Task RunPreviewAsync()
    {
        if (_preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        if (SelectedStrategy == StrategyKind.Onnx && !Onnx.IsModelReady)
        {
            return;
        }
        if (SelectedStrategy == StrategyKind.Sam && (!Sam.IsModelReady || _samEmbedding is null || _samPromptPointPreview is null))
        {
            return;
        }
        if (SelectedStrategy == StrategyKind.MagicWand && _magicWandSeedPreview is null)
        {
            return;
        }

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        try
        {
            // Snapshot the scribble masks on the UI thread: background preview threads must
            // never touch the manager's live Mats, which the UI disposes on stroke/undo/clear.
            using var fgScribble = ScribbleManager.SnapshotForegroundScribble();
            using var bgScribble = ScribbleManager.SnapshotBackgroundScribble();
            var context = BuildContext(grabCutFg: fgScribble, grabCutBg: bgScribble);

            // Snapshot the (small) preview Mat on the UI thread: loading another image or
            // closing the tab disposes _preview while a run may still be in flight.
            using var previewBgr = _preview.Bgr.Clone();
            var result = await strategy.RunPreviewAsync(previewBgr, context, cts.Token);

            if (cts.IsCancellationRequested)
            {
                result.Dispose();
                return;
            }

            _lastPreviewResult?.Dispose();
            _lastPreviewResult = result;
            ResultBitmap = result.Bgra.ToBitmapSource();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer preview request
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs the selected strategy at full resolution with the same parameters the preview
    /// uses, so the result is faithful to what the user saw. For GrabCut, the current
    /// foreground/background scribbles (resized to full resolution) are included in the
    /// context so they feed the same single mask computation the preview used.
    /// </summary>
    private async Task<RemovalResult> RunStrategyFullAsync(IBackgroundRemovalStrategy strategy, CancellationToken ct)
    {
        if (_loadedImage is null || _preview is null)
        {
            throw new InvalidOperationException("No image loaded.");
        }

        // Full-res scribble copies must stay alive for the whole background run. Declaring
        // them with "using var" inside the if would dispose them at the closing brace --
        // before RunFullAsync even starts -- which surfaced as "Cannot access a disposed
        // object" on apply/export. They are declared here, in the method scope.
        using var fgFull = SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles
            ? ScribbleManager.ForegroundScribble?.ResizeScribble(_loadedImage.FullBgr.Size())
            : null;
        using var bgFull = SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles
            ? ScribbleManager.BackgroundScribble?.ResizeScribble(_loadedImage.FullBgr.Size())
            : null;
        var context = BuildContext(_preview.ScaleFactor, fgFull, bgFull);

        // Snapshot the source on the UI thread: the run happens on a worker and undo/redo or
        // loading another image can dispose _loadedImage mid-run -- the run must never read
        // the live Mat after that (it previously surfaced as "Cannot access a disposed
        // object" on apply/export).
        using var fullBgr = _loadedImage.FullBgr.Clone();
        return await strategy.RunFullAsync(fullBgr, context, ct);
    }

    /// <summary>True when the working result is authoritative and must be kept as-is on export.</summary>
    private bool IsWorkingResultAuthoritative => _workingResultIsLoadedCutout || _workingResultHandEdited;

    /// <summary>
    /// Ensures a full-resolution working result exists, recomputing it (faithful to the live
    /// preview) on demand. Authoritative results (loaded cutouts, hand-edited results) are
    /// kept untouched. Returns true when a working result is available afterwards.
    /// </summary>
    private async Task<bool> EnsureWorkingResultAsync()
    {
        if (_workingBgr is not null && _workingAlpha is not null && IsWorkingResultAuthoritative)
        {
            return true;
        }
        if (_loadedImage is null || _preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            StatusMessage = "Choose an image first.";
            return false;
        }

        _processCts?.Cancel();
        var cts = new CancellationTokenSource();
        _processCts = cts;

        try
        {
            IsBusy = true;
            BusyMessage = "Processing at full resolution...";
            var result = await RunStrategyFullAsync(strategy, cts.Token);
            SetWorkingResult(result);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled.";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Processing failed: {ex.Message}";
            _log.Error("Full-resolution processing failed", ex);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetWorkingResult(RemovalResult result)
    {
        DisposeWorkingResult();

        (_workingBgr, _workingAlpha) = BackgroundCompositingService.SplitBgra(result.Bgra);
        result.Dispose();

        _history.Clear();
        RefreshUndoRedoState();
        OnPropertyChanged(nameof(HasWorkingResult));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        IsDirty = true; // freshly computed, not yet saved as a work file
        RefreshResultBitmapFromWorking();
    }

    private void RefreshResultBitmapFromWorking()
    {
        if (_workingBgr is null || _workingAlpha is null)
        {
            return;
        }
        ResultBitmap = _workingBgr.ToBitmapSource(_workingAlpha);
    }

    private void DisposeWorkingResult()
    {
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
        _workingBgr = null;
        _workingAlpha = null;
        _workingResultIsLoadedCutout = false;
        _workingResultHandEdited = false;
        IsDirty = false;
        ExportCommand.NotifyCanExecuteChanged();
    }
}
