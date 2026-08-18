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

    private Mat? _grabCutFgScribble;
    private Mat? _grabCutBgScribble;
    private WpfPoint? _scribbleLastPoint;
    private readonly Stack<(Mat Fg, Mat Bg)> _scribbleUndo = new();
    private readonly Stack<(Mat Fg, Mat Bg)> _scribbleRedo = new();

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
                ClearScribbles();
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
            ComputeSamEmbedding();
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
        if (_sourceImage is null || !Sam.IsModelReady)
        {
            return;
        }
        try
        {
            _samEmbedding = _samStrategy.ComputeEmbedding(_sourceImage.FullBgr);
        }
        catch (Exception ex)
        {
            Sam.ErrorMessage = $"Embedding failed: {ex.Message}";
            _log.Error("SAM embedding computation failed", ex);
        }
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
                GrabCutForegroundScribble = scaleToFull == 1.0 ? _grabCutFgScribble : null,
                GrabCutBackgroundScribble = scaleToFull == 1.0 ? _grabCutBgScribble : null,
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
        EnsureScribbleMats();
        PushScribbleUndoSnapshot();
        _scribbleLastPoint = imagePoint;
        DrawScribbleSegment(imagePoint, imagePoint);
    }

    public void OnOriginalStrokeMove(WpfPoint imagePoint)
    {
        if (_scribbleLastPoint is not { } last)
        {
            return;
        }
        DrawScribbleSegment(last, imagePoint);
        _scribbleLastPoint = imagePoint;
    }

    public void OnOriginalStrokeEnd()
    {
        _scribbleLastPoint = null;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
    }

    private void EnsureScribbleMats()
    {
        if (_preview is null) return;
        var size = _preview.Bgr.Size();
        _grabCutFgScribble ??= new Mat(size, MatType.CV_8UC1, Scalar.All(0));
        _grabCutBgScribble ??= new Mat(size, MatType.CV_8UC1, Scalar.All(0));
    }

    private const int MaxScribbleHistoryDepth = 20;

    private void PushScribbleUndoSnapshot()
    {
        if (_grabCutFgScribble is null || _grabCutBgScribble is null) return;
        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        _scribbleUndo.TrimStack(MaxScribbleHistoryDepth, drop =>
        {
            drop.Fg.Dispose();
            drop.Bg.Dispose();
        });
        foreach (var (f, b) in _scribbleRedo) { f.Dispose(); b.Dispose(); }
        _scribbleRedo.Clear();
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
    }


    private void DrawScribbleSegment(WpfPoint from, WpfPoint to)
    {
        if (_grabCutFgScribble is null || _grabCutBgScribble is null) return;
        var p1 = new Point((int)Math.Round(from.X), (int)Math.Round(from.Y));
        var p2 = new Point((int)Math.Round(to.X), (int)Math.Round(to.Y));
        const int thickness = 6;
        if (OriginalMode == InteractionMode.ScribbleForeground)
        {
            Cv2.Line(_grabCutFgScribble, p1, p2, Scalar.All(255), thickness, LineTypes.AntiAlias);
            Cv2.Line(_grabCutBgScribble, p1, p2, Scalar.All(0), thickness, LineTypes.AntiAlias);
        }
        else if (OriginalMode == InteractionMode.ScribbleBackground)
        {
            Cv2.Line(_grabCutBgScribble, p1, p2, Scalar.All(255), thickness, LineTypes.AntiAlias);
            Cv2.Line(_grabCutFgScribble, p1, p2, Scalar.All(0), thickness, LineTypes.AntiAlias);
        }
    }

    private bool HasNonEmptyScribbles()
    {
        return (_grabCutFgScribble is not null && Cv2.CountNonZero(_grabCutFgScribble) > 0)
            || (_grabCutBgScribble is not null && Cv2.CountNonZero(_grabCutBgScribble) > 0);
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !HasNonEmptyScribbles())
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

    private bool CanUndoScribble => _scribbleUndo.Count > 0;
    private bool CanRedoScribble => _scribbleRedo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndoScribble))]
    public void UndoScribble()
    {
        if (_scribbleUndo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null) return;
        _scribbleRedo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleUndo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
        ScribbleStrokeUndone?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanRedoScribble))]
    public void RedoScribble()
    {
        if (_scribbleRedo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null) return;
        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleRedo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
        ScribbleStrokeRedone?.Invoke(this, EventArgs.Empty);
    }

    private void ClearScribbles()
    {
        _grabCutFgScribble?.Dispose();
        _grabCutBgScribble?.Dispose();
        _grabCutFgScribble = null;
        _grabCutBgScribble = null;
        foreach (var (f, b) in _scribbleUndo) { f.Dispose(); b.Dispose(); }
        foreach (var (f, b) in _scribbleRedo) { f.Dispose(); b.Dispose(); }
        _scribbleUndo.Clear();
        _scribbleRedo.Clear();
        GrabCut.HasScribbles = false;
        UndoScribbleCommand.NotifyCanExecuteChanged();
        RedoScribbleCommand.NotifyCanExecuteChanged();
        ScribblesCleared?.Invoke(this, EventArgs.Empty);
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
            if (SelectedStrategy == StrategyKind.GrabCut && HasNonEmptyScribbles())
            {
                using var fgFull = _grabCutFgScribble.ResizeScribble(_sourceImage.FullBgr.Size());
                using var bgFull = _grabCutBgScribble.ResizeScribble(_sourceImage.FullBgr.Size());
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
        ClearScribbles();
    }
}
