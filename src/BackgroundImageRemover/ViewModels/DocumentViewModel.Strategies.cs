using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    private void RequestPreviewDebounced() => _previews.RequestPreviewDebounced();

    private Task RunPreviewAsync() => _previews.RunPreviewAsync();

    /// <summary>True when the selected strategy has everything a preview run needs.</summary>
    private bool IsPreviewReady(StrategyKind kind) => kind switch
    {
        StrategyKind.Onnx => Onnx.IsModelReady,
        StrategyKind.Sam => Sam.IsModelReady && _samEmbedding is not null && _samPromptPointPreview is not null,
        StrategyKind.MagicWand => _magicWandSeedPreview is not null,
        _ => true
    };

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
        FinalizeWorkingState(
            markDirty: false, // the loaded cutout matches the file on disk until it is edited
            notifyCommandAvailability: true,
            status: $"Loaded cutout ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})");
    }

    /// <summary>
    /// Runs the selected strategy at full resolution with the same parameters the preview
    /// uses, so the result is faithful to what the user saw. For GrabCut, the current
    /// foreground/background scribbles (resized to full resolution) are included in the
    /// context so they feed the same single mask computation the preview used.
    /// </summary>
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

        return await _fullRes.RunAsync(
            strategy,
            busyMessage: "Processing at full resolution...",
            cancelledStatus: "Processing cancelled.",
            failureStatusPrefix: "Processing failed",
            onFailure: ex => _log.Error("Full-resolution processing failed", ex),
            handleResult: result =>
            {
                SetWorkingResult(result);
                return true;
            });
    }

    private void SetWorkingResult(RemovalResult result)
    {
        DisposeWorkingResult();

        (_workingBgr, _workingAlpha) = BackgroundCompositingService.SplitBgra(result.Bgra);
        result.Dispose();

        _history.Clear();
        // Freshly computed, not yet saved as a work file.
        FinalizeWorkingState(markDirty: true, notifyCommandAvailability: true);
    }

    private void RefreshResultBitmapFromWorking()
        => ResultBitmap = _workingBgr.ToResultBitmap(_workingAlpha);

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
