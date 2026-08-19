using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    private void RequestPreviewDebounced()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
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

            // Snapshot the (small) preview Mat on the UI thread: closing the tab or loading a
            // new image disposes _preview while a run may still be in flight.
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
            IsDirty = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    private StrategyContext BuildContext(double scaleToFull = 1.0, Mat? grabCutFg = null, Mat? grabCutBg = null)
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
                // The caller passes ownership-transferred snapshots (preview) or full-res
                // resized copies (apply) that stay valid for the whole background run -- never
                // the manager's live Mats, which the UI thread may dispose mid-run.
                GrabCutForegroundScribble = grabCutFg,
                GrabCutBackgroundScribble = grabCutBg,
                GrabCutIterations = 3,
                GrabCutFeatherPixels = Math.Max(1, (int)Math.Round(2 * scaleToFull))
            },
            StrategyKind.Onnx => new StrategyContext
            {
                OnnxModel = Onnx.SelectedModel,
                OnnxFeatherPixels = (int)Math.Round(Onnx.FeatherPixels * scaleToFull),
                EnableAlphaMatting = Onnx.EnableAlphaMatting
            },
            StrategyKind.Sam => new StrategyContext
            {
                SamPromptPoint = _samPromptPointPreview is { } p
                    ? new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))
                    : (Point?)null,
                SamPromptPoints = _samPromptPointsPreview?.Select(p =>
                    new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))).ToArray(),
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
            DespeckleKernelSize = (int)Math.Round(DespeckleKernelSize * scaleToFull),
            FillHolesKernelSize = (int)Math.Round(FillHolesKernelSize * scaleToFull),
            SmoothEdgesKernelSize = (int)Math.Round(SmoothEdgesKernelSize * scaleToFull),
            KeepLargestComponent = KeepLargestComponent,
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

    public void OnOriginalWandClicked(Point imagePoint)
    {
        // Sample-color mode: pick the clicked pixel as the Chroma Key background color.
        if (SampleColorMode)
        {
            if (_preview is null)
            {
                return;
            }
            var px = _preview.Bgr.At<Vec3b>(imagePoint.Y, imagePoint.X);
            ChromaKey.DetectedColorBgr = new Vec3b(px.Item0, px.Item1, px.Item2);
            SampleColorMode = false;
            SelectedStrategy = StrategyKind.ChromaKey;
            RequestPreviewDebounced();
            return;
        }

        if (SelectedStrategy != StrategyKind.MagicWand)
        {
            return;
        }
        _magicWandSeedPreview = new WpfPoint(imagePoint.X, imagePoint.Y);
        MagicWand.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    public void OnOriginalSamPointClicked(Point imagePoint)
    {
        if (SelectedStrategy != StrategyKind.Sam)
        {
            return;
        }
        _samPromptPointPreview = new WpfPoint(imagePoint.X, imagePoint.Y);
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    /// <summary>
    /// Adds an additional foreground point for SAM segmentation. Multiple points refine the
    /// selection: the primary click plus any added points all feed the decoder together.
    /// </summary>
    public void OnOriginalSamAdditionalPointClicked(Point imagePoint)
    {
        if (SelectedStrategy != StrategyKind.Sam)
        {
            return;
        }
        _samPromptPointsPreview ??= new List<WpfPoint>();
        _samPromptPointsPreview.Add(new WpfPoint(imagePoint.X, imagePoint.Y));
        Sam.AdditionalPointCount = _samPromptPointsPreview.Count;
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    /// <summary>Clears all SAM prompt points (both primary and additional).</summary>
    public void ClearSamPromptPoints()
    {
        _samPromptPointPreview = null;
        _samPromptPointsPreview?.Clear();
        Sam.AdditionalPointCount = 0;
        Sam.HasClickedPoint = false;
        RequestPreviewDebounced();
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !ScribbleManager.HasScribbles)
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
}
