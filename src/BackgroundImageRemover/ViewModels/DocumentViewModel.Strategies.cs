using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    private async Task EnsureOnnxReadyAsync()
    {
        var model = Onnx.SelectedModel;
        try
        {
            Onnx.ErrorMessage = null;
            Onnx.IsDownloading = true;
            var progress = new Progress<ModelDownloadProgress>(p => Onnx.DownloadFraction = p.FractionComplete);
            await _onnxStrategy.EnsureReadyAsync(model, progress, CancellationToken.None);
            if (model == Onnx.SelectedModel)
            {
                Onnx.IsModelReady = true;
                RequestPreviewDebounced();
            }
        }
        catch (Exception ex)
        {
            Onnx.ErrorMessage = $"Could not download model: {ex.Message}";
            _log.Error("ONNX model download failed", ex);
        }
        finally
        {
            Onnx.IsDownloading = false;
        }
    }

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private async Task EnsureSamReadyAsync()
    {
        try
        {
            Sam.ErrorMessage = null;
            Sam.IsDownloading = true;
            var progress = new Progress<ModelDownloadProgress>(p => Sam.DownloadFraction = p.FractionComplete);
            await _samStrategy.EnsureReadyAsync(progress, CancellationToken.None);
            Sam.IsModelReady = true;
            ExportCommand.NotifyCanExecuteChanged();
            if (_loadedImage is not null)
            {
                ComputeSamEmbedding();
            }
        }
        catch (Exception ex)
        {
            Sam.ErrorMessage = $"Could not download SAM model: {ex.Message}";
            _log.Error("SAM model download failed", ex);
        }
        finally
        {
            Sam.IsDownloading = false;
        }
    }

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        if (_loadedImage is null)
        {
            return;
        }
        try
        {
            _samEmbedding = _samStrategy.ComputeEmbedding(_loadedImage.FullBgr);
        }
        catch (Exception ex)
        {
            StatusMessage = $"SAM embedding failed: {ex.Message}";
            _log.Error("SAM embedding computation failed", ex);
        }
    }

    public void OnOriginalSamPointClicked(OpenCvSharp.Point previewPoint)
    {
        if (_samEmbedding is null)
        {
            StatusMessage = "SAM is still preparing this image, try again in a moment.";
            return;
        }
        _samPromptPointPreview = new WpfPoint(previewPoint.X, previewPoint.Y);
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    private void RequestPreviewDebounced()
    {
        if (!IsImageLoaded || ResultMode != InteractionMode.None)
        {
            return;
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private StrategyContext BuildContext(double scaleToFull = 1.0)
    {
        return SelectedStrategy switch
        {
            StrategyKind.ChromaKey => new StrategyContext
            {
                ChromaKeyColor = ChromaKey.DetectedColorBgr,
                ChromaKeyTolerance = ChromaKey.Tolerance,
                DecontaminateEdges = ChromaKey.SpillSuppression
            },
            StrategyKind.GrabCut => new StrategyContext
            {
                GrabCutRect = GrabCut.SelectedRect is { } r
                    ? new Rect(
                        (int)Math.Round(r.X * scaleToFull),
                        (int)Math.Round(r.Y * scaleToFull),
                        (int)Math.Round(r.Width * scaleToFull),
                        (int)Math.Round(r.Height * scaleToFull))
                    : (Rect?)null,
                // At preview scale (1.0), the scribbles are already in the right coordinate
                // space -- use them directly. A scaled-up (export) call overrides these with
                // resized copies; see RunStrategyFullAsync.
                GrabCutForegroundScribble = scaleToFull == 1.0 ? _grabCutFgScribble : null,
                GrabCutBackgroundScribble = scaleToFull == 1.0 ? _grabCutBgScribble : null,
                // Same iteration count as the preview, so the full-res result matches what the user saw.
                GrabCutIterations = 3,
                // Scale the feather with the resolution so the export keeps the same relative
                // softness the user saw in the preview.
                GrabCutFeatherPixels = Math.Max(1, (int)Math.Round(2 * scaleToFull))
            },
            StrategyKind.Onnx => new StrategyContext
            {
                OnnxModel = Onnx.SelectedModel,
                // Scale the feather with the resolution so the export keeps the same relative
                // softness the user saw in the preview.
                OnnxFeatherPixels = (int)Math.Round(Onnx.FeatherPixels * scaleToFull),
                EnableAlphaMatting = Onnx.EnableAlphaMatting
            },
            StrategyKind.Sam => new StrategyContext
            {
                SamPromptPoint = _samPromptPointPreview is { } p
                    ? new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))
                    : (Point?)null,
                SamEmbedding = _samEmbedding
            },
            _ => new StrategyContext()
        };
    }

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

        _editHistory.Clear();
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

        if (SelectedStrategy == StrategyKind.GrabCut && !GrabCut.HasValidRect && !HasNonEmptyScribbles())
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

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        try
        {
            var context = BuildContext();
            var result = await strategy.RunPreviewAsync(_preview.Bgr, context, cts.Token);

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

        var context = BuildContext(_preview.ScaleFactor);

        if (SelectedStrategy == StrategyKind.GrabCut && HasNonEmptyScribbles())
        {
            using var fgFull = _grabCutFgScribble.ResizeScribble(_loadedImage.FullBgr.Size());
            using var bgFull = _grabCutBgScribble.ResizeScribble(_loadedImage.FullBgr.Size());
            context = context with { GrabCutForegroundScribble = fgFull, GrabCutBackgroundScribble = bgFull };
            return await strategy.RunFullAsync(_loadedImage.FullBgr, context, ct);
        }

        return await strategy.RunFullAsync(_loadedImage.FullBgr, context, ct);
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

        _editHistory.Clear();
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
        using var bgra = new Mat();
        Cv2.CvtColor(_workingBgr, bgra, ColorConversionCodes.BGR2BGRA);
        BackgroundCompositingService.ReplaceAlphaChannel(bgra, _workingAlpha);
        ResultBitmap = bgra.ToBitmapSource();
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
