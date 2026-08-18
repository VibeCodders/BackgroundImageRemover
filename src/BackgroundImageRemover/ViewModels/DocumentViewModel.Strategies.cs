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

    private StrategyContext BuildContext(double scaleToFull = 1.0)
    {
        var strategyContext = SelectedStrategy switch
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
                GrabCutForegroundScribble = scaleToFull == 1.0 ? ScribbleManager.ForegroundScribble : null,
                GrabCutBackgroundScribble = scaleToFull == 1.0 ? ScribbleManager.BackgroundScribble : null,
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
            StrategyKind.FloodFill => new StrategyContext
            {
                FloodFillTolerance = FloodFill.Tolerance
            },
            StrategyKind.KMeans => new StrategyContext
            {
                KMeansClusters = KMeans.ClusterCount
            },
            StrategyKind.MagicWand => new StrategyContext
            {
                MagicWandSeed = _magicWandSeedPreview is { } p
                    ? new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))
                    : (Point?)null,
                MagicWandTolerance = MagicWand.Tolerance
            },
            StrategyKind.Otsu => new StrategyContext(),
            _ => new StrategyContext()
        };

        return strategyContext with
        {
            InvertMask = InvertMask,
            MaskFeatherPixels = (int)Math.Round(MaskFeatherPixels * scaleToFull),
            MaskExpandPixels = (int)Math.Round(MaskExpandPixels * scaleToFull),
            MaskBlurPixels = MaskBlurPixels * scaleToFull,
            MinComponentAreaPixels = (int)Math.Round(MinComponentAreaPixels * scaleToFull * scaleToFull),
            MaskGamma = MaskGamma,
            MaskHardness = MaskHardness,
            MaskThreshold = MaskThreshold,
            DespillStrength = DespillStrength,
            MaskMedianKernel = (int)Math.Round(MaskMedianKernel * scaleToFull),
            MaskBilateralKernel = (int)Math.Round(MaskBilateralKernel * scaleToFull),
            MaskClahe = MaskClahe
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

        _editSession.Clear();
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

        if (SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles)
        {
            using var fgFull = ScribbleManager.ForegroundScribble?.ResizeScribble(_loadedImage.FullBgr.Size());
            using var bgFull = ScribbleManager.BackgroundScribble?.ResizeScribble(_loadedImage.FullBgr.Size());
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

        _editSession.Clear();
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
