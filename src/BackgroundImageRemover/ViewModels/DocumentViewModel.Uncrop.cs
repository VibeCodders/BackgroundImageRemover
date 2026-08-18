using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    public UncropOptionsViewModel UncropOptions { get; } = new();

    // --- Uncrop Commands ---
    private bool CanApplyUncrop() => IsImageLoaded && !IsBusy && UncropOptions.CanExecute();

    private bool CanCancelUncrop() => IsBusy && _uncropCts is not null && !_uncropCts.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanCancelUncrop))]
    private void CancelUncrop()
    {
        if (_uncropCts is not null && !_uncropCts.IsCancellationRequested)
        {
            _uncropCts.Cancel();
            StatusMessage = "Cancelling uncrop operation...";
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyUncrop))]
    private async Task ApplyUncropAsync()
    {
        if (_loadedImage is null)
        {
            return;
        }

        var config = UncropOptions.ToConfig();

        _uncropCts?.Dispose();
        _uncropCts = new CancellationTokenSource();
        var ct = _uncropCts.Token;

        try
        {
            IsBusy = true;
            CancelUncropCommand.NotifyCanExecuteChanged();
            StatusMessage = "Applying uncrop expansion...";

            using var filledBgr = await UncropOperationHelper.ExecuteUncropAsync(
                _loadedImage.FullBgr, config, _uncropFillService, ct);

            // Apply finishing (flip, grain, border, rounded corners), then split back to BGR + alpha.
            using var finishedBgra = UncropOperationHelper.ApplyFinishing(filledBgr, config);
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(finishedBgra);

            // Create new LoadedImage from the finished result
            var newLoadedImage = new LoadedImage(_loadedImage.FilePath, bgr, alpha);
            _loadedImage?.Dispose();
            _preview?.Dispose();
            _loadedImage = newLoadedImage;

            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;
            PreviewBitmap = _loadedImage.FullAlpha is { } a
                ? preview.Bgr.BuildPreviewWithAlpha(a)
                : preview.Bgr.ToBitmapSource();

            // Set as new working image
            DisposeWorkingResult();
            _workingBgr = _loadedImage.FullBgr.Clone();
            _workingAlpha = _loadedImage.FullAlpha?.Clone()
                ?? new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
            _workingResultIsLoadedCutout = false;
            _workingResultHandEdited = true;

            _editSession.Clear();
            RefreshUndoRedoState();
            RefreshResultBitmapFromWorking();

            UncropOptions.Reset();
            IsDirty = true;
            StatusMessage = $"Applied {config.FillMode} uncrop ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height}).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Uncrop operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Uncrop failed: {ex.Message}";
            _log.Error("Uncrop failed", ex);
        }
        finally
        {
            _uncropCts?.Dispose();
            _uncropCts = null;
            IsBusy = false;
            CancelUncropCommand.NotifyCanExecuteChanged();
        }
    }
}
