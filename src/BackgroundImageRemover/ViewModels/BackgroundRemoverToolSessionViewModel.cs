using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels.StrategyViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for AI and algorithmic Background Removal (ONNX, SAM, GrabCut, ChromaKey).
/// </summary>
public partial class BackgroundRemoverToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IDownscaleService _downscaler;
    private readonly IDialogService _dialogs;
    private readonly IFileLogService _log;
    private readonly IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _processCts;

    private LoadedImage? _sourceImage;
    private PreviewImage? _preview;
    private RemovalResult? _lastPreviewResult;

    private readonly ScribbleManager _scribbleManager = new();
    internal ScribbleManager ScribbleManager => _scribbleManager; // Expose for partial classes

    private SamEmbedding? _samEmbedding;
    private WpfPoint? _samPromptPointPreview;

    public override string ToolBadge => "✂ Background Remover";
    public override string AccentColor => "#1E7A33";

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();
    public SamStrategyViewModel Sam { get; } = new();

    [ObservableProperty]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private InteractionMode _originalMode = InteractionMode.None;

    [ObservableProperty]
    private bool _useGpuForOnnx;

    public event EventHandler? ScribbleStrokeUndone;
    public event EventHandler? ScribbleStrokeRedone;
    public event EventHandler? ScribblesCleared;

    public BackgroundRemoverToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IDownscaleService downscaler,
        IDialogService dialogs,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy)
        : base(shell, parentDocument)
    {
        _downscaler = downscaler;
        _dialogs = dialogs;
        _log = log;
        _strategies = strategies.ToDictionary(s => s.Kind);
        _onnxStrategy = onnxStrategy;
        _grabCutStrategy = grabCutStrategy;
        _samStrategy = samStrategy;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await RunPreviewAsync();
        };

        // Subscribe to scribble manager events
        ScribbleManager.StrokeUndone += (_, _) => ScribbleStrokeUndone?.Invoke(this, EventArgs.Empty);
        ScribbleManager.StrokeRedone += (_, _) => ScribbleStrokeRedone?.Invoke(this, EventArgs.Empty);
        ScribbleManager.ScribblesCleared += (_, _) => ScribblesCleared?.Invoke(this, EventArgs.Empty);

        ChromaKey.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ChromaKey.Tolerance) or nameof(ChromaKey.SpillSuppression))
            {
                RequestPreviewDebounced();
            }
        };

        GrabCut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrabCut.SelectedRect))
            {
                ScribbleManager.Clear();
                GrabCut.HasScribbles = false;
                if (GrabCut.HasValidRect)
                {
                    RequestPreviewDebounced();
                }
            }
        };

        Onnx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Onnx.SelectedModel))
            {
                Onnx.IsModelReady = _onnxStrategy.IsReady(Onnx.SelectedModel);
                if (SelectedStrategy == StrategyKind.Onnx && !Onnx.IsModelReady)
                {
                    _ = EnsureOnnxReadyAsync();
                }
            }
            if (e.PropertyName is nameof(Onnx.FeatherPixels) or nameof(Onnx.EnableAlphaMatting) && Onnx.IsModelReady)
            {
                RequestPreviewDebounced();
            }
        };

        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        var preview = _downscaler.CreatePreview(_sourceImage.FullBgr);
        _preview = preview;

        bool isActualCutout = BackgroundCompositingService.HasMeaningfulTransparency(_sourceImage.FullAlpha);
        PreviewBitmap = isActualCutout
            ? preview.Bgr.BuildPreviewWithAlpha(_sourceImage.FullAlpha!)
            : preview.Bgr.ToBitmapSource();

        ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);
        Onnx.IsModelReady = _onnxStrategy.IsReady(Onnx.SelectedModel);
        Sam.IsModelReady = _samStrategy.IsReady;

        RequestPreviewDebounced();
    }

    partial void OnSelectedStrategyChanged(StrategyKind value)
    {
        OriginalMode = value switch
        {
            StrategyKind.GrabCut => InteractionMode.DrawRect,
            StrategyKind.Sam => InteractionMode.SamClick,
            _ => InteractionMode.None
        };

        if (value == StrategyKind.Onnx && !Onnx.IsModelReady)
        {
            _ = EnsureOnnxReadyAsync();
        }
        else if (value == StrategyKind.Sam)
        {
            if (!Sam.IsModelReady)
            {
                _ = EnsureSamReadyAsync();
            }
            else if (_samEmbedding is null)
            {
                ComputeSamEmbedding();
            }
        }

        RequestPreviewDebounced();
    }

    private async Task EnsureOnnxReadyAsync()
    {
        var model = Onnx.SelectedModel;
        Onnx.ErrorMessage = null;
        Onnx.IsDownloading = true;

        var success = await ModelDownloadHelper.EnsureOnnxModelReadyAsync(
            _onnxStrategy,
            model,
            progress => Onnx.DownloadFraction = progress,
            error => Onnx.ErrorMessage = error,
            () =>
            {
                if (model == Onnx.SelectedModel)
                {
                    Onnx.IsModelReady = true;
                    RequestPreviewDebounced();
                }
            },
            _log,
            CancellationToken.None);

        Onnx.IsDownloading = false;
    }

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private async Task EnsureSamReadyAsync()
    {
        Sam.ErrorMessage = null;
        Sam.IsDownloading = true;

        var success = await ModelDownloadHelper.EnsureSamModelReadyAsync(
            _samStrategy,
            progress => Sam.DownloadFraction = progress,
            error => Sam.ErrorMessage = error,
            () =>
            {
                Sam.IsModelReady = true;
                ComputeSamEmbedding();
            },
            _log,
            CancellationToken.None);

        Sam.IsDownloading = false;
    }

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        if (_sourceImage is null || !Sam.IsModelReady)
        {
            return;
        }
        _samEmbedding = ModelDownloadHelper.ComputeSamEmbeddingSafe(
            _samStrategy,
            _sourceImage.FullBgr,
            error => Sam.ErrorMessage = error,
            _log);
    }

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
            IsDirty = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
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
                GrabCutForegroundScribble = scaleToFull == 1.0 ? ScribbleManager.ForegroundScribble : null,
                GrabCutBackgroundScribble = scaleToFull == 1.0 ? ScribbleManager.BackgroundScribble : null,
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
                SamEmbedding = _samEmbedding
            },
            _ => new StrategyContext()
        };
    }

    // --- Scribble & interaction handlers ---

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

    [RelayCommand]
    private void SetOriginalScribbleMode(InteractionMode mode)
        => OriginalMode = OriginalMode == mode ? InteractionMode.DrawRect : mode;

    public void OnOriginalStrokeStart(WpfPoint imagePoint)
    {
        if (_preview is null) return;
        ScribbleManager.EnsureMats(_preview.Bgr.Size());

        var scribbleMode = OriginalMode == InteractionMode.ScribbleForeground
            ? ScribbleMode.Foreground
            : OriginalMode == InteractionMode.ScribbleBackground
                ? ScribbleMode.Background
                : ScribbleMode.Foreground; // fallback

        ScribbleManager.StartStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        var scribbleMode = OriginalMode == InteractionMode.ScribbleForeground
            ? ScribbleMode.Foreground
            : OriginalMode == InteractionMode.ScribbleBackground
                ? ScribbleMode.Background
                : ScribbleMode.Foreground; // fallback

        ScribbleManager.MoveStroke(imagePoint, scribbleMode);
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
    }

    public void OnOriginalStrokeEnd()
    {
        ScribbleManager.EndStroke();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
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

    private bool CanUndoScribble => ScribbleManager.CanUndo;
    private bool CanRedoScribble => ScribbleManager.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoScribble))]
    public void UndoScribble()
    {
        ScribbleManager.Undo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedoScribble))]
    public void RedoScribble()
    {
        ScribbleManager.Redo();
        GrabCut.HasScribbles = ScribbleManager.HasScribbles;
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
    }


    public override async Task ApplyAsync()
    {
        if (_sourceImage is null || _preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            _shell.CloseTabDirect(this);
            return;
        }

        _processCts?.Cancel();
        var cts = new CancellationTokenSource();
        _processCts = cts;

        bool succeeded = false;
        try
        {
            IsBusy = true;
            BusyMessage = "Computing full-resolution background removal...";

            var context = BuildContext(_preview.ScaleFactor);
            if (SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles)
            {
                using var fgFull = ScribbleManager.ForegroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size());
                using var bgFull = ScribbleManager.BackgroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size());
                context = context with { GrabCutForegroundScribble = fgFull, GrabCutBackgroundScribble = bgFull };
            }

            var fullResult = await strategy.RunFullAsync(_sourceImage.FullBgr, context, cts.Token);
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(fullResult.Bgra);
            fullResult.Dispose();

            _parentDocument.ApplyToolResult(bgr, alpha, $"Remove Background ({SelectedStrategy})");
            succeeded = true;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to apply background removal", ex);
            StatusMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            if (succeeded)
            {
                _shell.CloseTabDirect(this);
            }
        }
    }

    public override void Dispose()
    {
        _debounceTimer.Stop();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _processCts?.Cancel();
        _processCts?.Dispose();
        _sourceImage?.Dispose();
        _preview?.Dispose();
        _lastPreviewResult?.Dispose();
        _samEmbedding = null;
        ScribbleManager.Dispose();
    }
}
