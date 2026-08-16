using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Refinement;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels.StrategyViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// State and logic for a single open image ("document"): one per tab. <see cref="ShellViewModel"/>
/// owns a collection of these.
/// </summary>
public partial class DocumentViewModel : ObservableObject, IDisposable
{
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IDownscaleService _downscaler;
    private readonly IDialogService _dialogs;
    private readonly IBatchProcessingService _batchProcessor;
    private readonly ISettingsService _settings;
    private readonly IFileLogService _log;
    private readonly IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;
    private readonly EditHistory _editHistory = new();

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _applyCts;

    private LoadedImage? _loadedImage;
    private PreviewImage? _preview;
    private RemovalResult? _lastPreviewResult;

    // The "working" composited result: BGR color (may include chroma-key spill correction) and
    // alpha kept apart so Undo/Redo, Brush and Magic Wand can mutate just the alpha cheaply.
    private Mat? _workingBgr;
    private Mat? _workingAlpha;

    private Mat? _grabCutFgScribble;
    private Mat? _grabCutBgScribble;
    private WpfPoint? _scribbleLastPoint;
    private WpfPoint? _brushLastPoint;
    private readonly Stack<(Mat Fg, Mat Bg)> _scribbleUndo = new();
    private readonly Stack<(Mat Fg, Mat Bg)> _scribbleRedo = new();

    private SamEmbedding? _samEmbedding;
    private WpfPoint? _samPromptPointPreview;

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();
    public SamStrategyViewModel Sam { get; } = new();

    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    private bool _isImageLoaded;

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
    [NotifyPropertyChangedFor(nameof(IsResultEditModeActive))]
    private InteractionMode _resultMode = InteractionMode.None;

    [ObservableProperty]
    private bool _isCompareMode;

    [ObservableProperty]
    private double _brushRadius = 20;

    [ObservableProperty]
    private double _brushHardness = 0.5;

    [ObservableProperty]
    private BrushMode _brushMode = BrushMode.Restore;

    [ObservableProperty]
    private double _magicWandTolerance = 20;

    [ObservableProperty]
    private double _compareDividerPosition = 0.5;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private ExportBackgroundMode _exportBackgroundMode = ExportBackgroundMode.Transparent;

    [ObservableProperty]
    private WpfColor _exportSolidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private string? _exportBackgroundImagePath;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private string? _batchStatus;

    [ObservableProperty]
    private bool _useGpuForOnnx;

    public bool HasWorkingResult => _workingAlpha is not null;
    public bool IsResultEditModeActive => ResultMode != InteractionMode.None;

    /// <summary>Raised after a scribble stroke is undone/redone, so the View can keep its stroke visuals in sync.</summary>
    public event EventHandler? ScribbleStrokeUndone;
    public event EventHandler? ScribbleStrokeRedone;

    /// <summary>Raised when scribbles are reset (new image, new rect), so the View can clear stroke visuals.</summary>
    public event EventHandler? ScribblesCleared;

    public DocumentViewModel(
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IDownscaleService downscaler,
        IDialogService dialogs,
        IBatchProcessingService batchProcessor,
        ISettingsService settings,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy)
    {
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _downscaler = downscaler;
        _dialogs = dialogs;
        _batchProcessor = batchProcessor;
        _settings = settings;
        _log = log;
        _strategies = strategies.ToDictionary(s => s.Kind);
        _onnxStrategy = onnxStrategy;
        _grabCutStrategy = grabCutStrategy;
        _samStrategy = samStrategy;

        _useGpuForOnnx = settings.Current.UseGpuForOnnx;
        _onnxStrategy.SetUseGpu(_useGpuForOnnx);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = RunPreviewAsync();
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
                ApplyCommand.NotifyCanExecuteChanged();
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
            if (e.PropertyName == nameof(Onnx.IsModelReady))
            {
                ApplyCommand.NotifyCanExecuteChanged();
                BatchCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName is nameof(Onnx.FeatherPixels) or nameof(Onnx.EnableAlphaMatting) && Onnx.IsModelReady)
            {
                RequestPreviewDebounced();
            }
        };
    }

    partial void OnOriginalModeChanged(InteractionMode value) => RefreshUndoRedoState();

    partial void OnUseGpuForOnnxChanged(bool value)
    {
        _onnxStrategy.SetUseGpu(value);
        _settings.Current.UseGpuForOnnx = value;
        _settings.Save();
        if (SelectedStrategy == StrategyKind.Onnx)
        {
            Onnx.IsModelReady = false;
            _ = EnsureOnnxReadyAsync();
        }
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
        else if (value == StrategyKind.Sam && !Sam.IsModelReady)
        {
            _ = EnsureSamReadyAsync();
        }
        else
        {
            RequestPreviewDebounced();
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            await LoadImageAsync(path);
        }
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading image...";

            _loadedImage?.Dispose();
            _preview?.Dispose();
            DisposeWorkingResult();
            _editHistory.Clear();
            RefreshUndoRedoState();
            _samEmbedding = null;
            _samPromptPointPreview = null;
            Sam.HasClickedPoint = false;

            _loadedImage = await _imageLoader.LoadAsync(path);
            _preview = _downscaler.CreatePreview(_loadedImage.FullBgr);

            PreviewBitmap = _preview.Bgr.ToBitmapSource();
            ResultBitmap = null;
            IsImageLoaded = true;
            Title = Path.GetFileName(path);
            StatusMessage = $"Loaded {Path.GetFileName(path)} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
            _log.Info($"Loaded image {path} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})");
            _settings.AddRecentFile(path);

            GrabCut.SelectedRect = null;
            ClearScribbles();
            ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);

            if (SelectedStrategy == StrategyKind.Sam && Sam.IsModelReady)
            {
                ComputeSamEmbedding();
            }

            RequestPreviewDebounced();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            _log.Error($"Could not load image: {path}", ex);
        }
        finally
        {
            IsBusy = false;
        }
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
            ApplyCommand.NotifyCanExecuteChanged();
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
                ChromaKeySpillSuppression = ChromaKey.SpillSuppression
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
                GrabCutIterations = scaleToFull > 1.0 ? 5 : 3
            },
            StrategyKind.Onnx => new StrategyContext
            {
                OnnxModel = Onnx.SelectedModel,
                OnnxFeatherPixels = Onnx.FeatherPixels,
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

    private async Task RunPreviewAsync()
    {
        if (_preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        if (SelectedStrategy == StrategyKind.GrabCut && !GrabCut.HasValidRect)
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

    private bool CanApply() => IsImageLoaded && !IsBusy
        && (SelectedStrategy != StrategyKind.GrabCut || GrabCut.HasValidRect)
        && (SelectedStrategy != StrategyKind.Onnx || Onnx.IsModelReady)
        && (SelectedStrategy != StrategyKind.Sam || (Sam.IsModelReady && Sam.HasClickedPoint));

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (_loadedImage is null || _preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        _applyCts?.Cancel();
        var cts = new CancellationTokenSource();
        _applyCts = cts;

        try
        {
            IsBusy = true;
            BusyMessage = "Processing at full resolution...";

            var context = BuildContext(_preview.ScaleFactor);
            var result = await strategy.RunFullAsync(_loadedImage.FullBgr, context, cts.Token);

            if (SelectedStrategy == StrategyKind.GrabCut && HasNonEmptyScribbles() && _grabCutStrategy.LastLabelMask is { } fullLabelMask)
            {
                using var fgFull = ResizeScribbleToSize(_grabCutFgScribble, _loadedImage.FullBgr.Size());
                using var bgFull = ResizeScribbleToSize(_grabCutBgScribble, _loadedImage.FullBgr.Size());
                using var refinedAlpha = _grabCutStrategy.RefineWithScribbles(_loadedImage.FullBgr, fullLabelMask, fgFull, bgFull, iterations: 3);
                ReplaceAlphaChannel(result.Bgra, refinedAlpha);
            }

            SetWorkingResult(result);
            StatusMessage = $"Processed in {result.ElapsedMilliseconds:F0} ms";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Processing failed: {ex.Message}";
            _log.Error("Apply failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void ReplaceAlphaChannel(Mat bgra, Mat newAlpha)
    {
        var channels = Cv2.Split(bgra);
        try
        {
            newAlpha.CopyTo(channels[3]);
            Cv2.Merge(channels, bgra);
        }
        finally
        {
            foreach (var c in channels) c.Dispose();
        }
    }

    private void SetWorkingResult(RemovalResult result)
    {
        DisposeWorkingResult();

        var channels = Cv2.Split(result.Bgra);
        try
        {
            _workingBgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, _workingBgr);
            _workingAlpha = channels[3].Clone();
        }
        finally
        {
            foreach (var c in channels) c.Dispose();
        }
        result.Dispose();

        _editHistory.Clear();
        RefreshUndoRedoState();
        OnPropertyChanged(nameof(HasWorkingResult));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
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
        ReplaceAlphaChannel(bgra, _workingAlpha);
        ResultBitmap = bgra.ToBitmapSource();
    }

    private void DisposeWorkingResult()
    {
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
        _workingBgr = null;
        _workingAlpha = null;
    }

    // --- Undo / Redo: while scribbling, undoes the last scribble stroke; otherwise undoes
    // the last brush/magic-wand edit on the working alpha channel. ---

    private bool IsScribbling => OriginalMode is InteractionMode.ScribbleForeground or InteractionMode.ScribbleBackground;

    private bool CanUndoExecute() => IsScribbling ? _scribbleUndo.Count > 0 : _editHistory.CanUndo;
    private bool CanRedoExecute() => IsScribbling ? _scribbleRedo.Count > 0 : _editHistory.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (IsScribbling && TryUndoScribble())
        {
            ScribbleStrokeUndone?.Invoke(this, EventArgs.Empty);
            RefreshUndoRedoState();
            return;
        }

        if (_workingAlpha is null)
        {
            return;
        }
        var restored = _editHistory.Undo(_workingAlpha);
        if (restored is null)
        {
            return;
        }
        _workingAlpha.Dispose();
        _workingAlpha = restored;
        RefreshUndoRedoState();
        RefreshResultBitmapFromWorking();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (IsScribbling && TryRedoScribble())
        {
            ScribbleStrokeRedone?.Invoke(this, EventArgs.Empty);
            RefreshUndoRedoState();
            return;
        }

        if (_workingAlpha is null)
        {
            return;
        }
        var restored = _editHistory.Redo(_workingAlpha);
        if (restored is null)
        {
            return;
        }
        _workingAlpha.Dispose();
        _workingAlpha = restored;
        RefreshUndoRedoState();
        RefreshResultBitmapFromWorking();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // --- Result-pane refinement: brush and magic wand, operating on the working alpha ---

    [RelayCommand]
    private void SetResultMode(InteractionMode mode) => ResultMode = ResultMode == mode ? InteractionMode.None : mode;

    public void OnResultStrokeStart(WpfPoint imagePoint)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        _editHistory.Push(_workingAlpha);
        RefreshUndoRedoState();
        _brushLastPoint = imagePoint;
        StampBrush(imagePoint, imagePoint);
    }

    public void OnResultStrokeMove(WpfPoint imagePoint)
    {
        if (_workingAlpha is null || _brushLastPoint is not { } last)
        {
            return;
        }
        StampBrush(last, imagePoint);
        _brushLastPoint = imagePoint;
    }

    public void OnResultStrokeEnd() => _brushLastPoint = null;

    private void StampBrush(WpfPoint from, WpfPoint to)
    {
        if (_workingAlpha is null)
        {
            return;
        }
        BrushEditor.StampSegment(_workingAlpha,
            new Point2f((float)from.X, (float)from.Y), new Point2f((float)to.X, (float)to.Y),
            BrushRadius, BrushHardness, BrushMode);
        RefreshResultBitmapFromWorking();
    }

    public void OnResultWandClicked(Point imagePoint)
    {
        if (_workingAlpha is null || _workingBgr is null)
        {
            return;
        }
        _editHistory.Push(_workingAlpha);
        RefreshUndoRedoState();
        MagicWandService.Apply(_workingBgr, _workingAlpha, imagePoint, MagicWandTolerance, add: BrushMode == BrushMode.Restore);
        RefreshResultBitmapFromWorking();
    }

    // --- Original-pane GrabCut scribble refinement ---

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

    public void OnOriginalStrokeEnd() => _scribbleLastPoint = null;

    private void DrawScribbleSegment(WpfPoint from, WpfPoint to)
    {
        var target = OriginalMode == InteractionMode.ScribbleForeground ? _grabCutFgScribble
            : OriginalMode == InteractionMode.ScribbleBackground ? _grabCutBgScribble
            : null;
        if (target is null)
        {
            return;
        }
        Cv2.Line(target, new Point((int)from.X, (int)from.Y), new Point((int)to.X, (int)to.Y), Scalar.All(255), thickness: 6);
        GrabCut.HasScribbles = HasNonEmptyScribbles();
    }

    private void EnsureScribbleMats()
    {
        if (_preview is null)
        {
            return;
        }
        _grabCutFgScribble ??= new Mat(_preview.Bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        _grabCutBgScribble ??= new Mat(_preview.Bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
    }

    private bool HasNonEmptyScribbles()
        => (_grabCutFgScribble is not null && Cv2.CountNonZero(_grabCutFgScribble) > 0)
        || (_grabCutBgScribble is not null && Cv2.CountNonZero(_grabCutBgScribble) > 0);

    private void ClearScribbles()
    {
        _grabCutFgScribble?.Dispose();
        _grabCutBgScribble?.Dispose();
        _grabCutFgScribble = null;
        _grabCutBgScribble = null;
        GrabCut.HasScribbles = false;

        foreach (var (fg, bg) in _scribbleUndo) { fg.Dispose(); bg.Dispose(); }
        foreach (var (fg, bg) in _scribbleRedo) { fg.Dispose(); bg.Dispose(); }
        _scribbleUndo.Clear();
        _scribbleRedo.Clear();
        RefreshUndoRedoState();
        ScribblesCleared?.Invoke(this, EventArgs.Empty);
    }

    private const int MaxScribbleHistoryDepth = 20;

    private void PushScribbleUndoSnapshot()
    {
        if (_grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return;
        }

        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        while (_scribbleUndo.Count > MaxScribbleHistoryDepth)
        {
            var items = _scribbleUndo.ToArray();
            _scribbleUndo.Clear();
            for (int i = items.Length - 2; i >= 0; i--) _scribbleUndo.Push(items[i]);
            items[^1].Fg.Dispose();
            items[^1].Bg.Dispose();
        }

        foreach (var (fg, bg) in _scribbleRedo) { fg.Dispose(); bg.Dispose(); }
        _scribbleRedo.Clear();
        RefreshUndoRedoState();
    }

    private bool TryUndoScribble()
    {
        if (_scribbleUndo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return false;
        }
        _scribbleRedo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleUndo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        return true;
    }

    private bool TryRedoScribble()
    {
        if (_scribbleRedo.Count == 0 || _grabCutFgScribble is null || _grabCutBgScribble is null)
        {
            return false;
        }
        _scribbleUndo.Push((_grabCutFgScribble.Clone(), _grabCutBgScribble.Clone()));
        var (fg, bg) = _scribbleRedo.Pop();
        _grabCutFgScribble.Dispose();
        _grabCutBgScribble.Dispose();
        _grabCutFgScribble = fg;
        _grabCutBgScribble = bg;
        GrabCut.HasScribbles = HasNonEmptyScribbles();
        return true;
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !HasNonEmptyScribbles() || _grabCutStrategy.LastLabelMask is not { } labelMask)
        {
            StatusMessage = "Draw the initial rectangle first, then add scribbles to refine.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Refining selection...";
            var refined = await Task.Run(() =>
                _grabCutStrategy.RefineWithScribbles(_preview.Bgr, labelMask, _grabCutFgScribble, _grabCutBgScribble, iterations: 3));

            using var bgra = new Mat();
            Cv2.CvtColor(_preview.Bgr, bgra, ColorConversionCodes.BGR2BGRA);
            ReplaceAlphaChannel(bgra, refined);
            refined.Dispose();
            ResultBitmap = bgra.ToBitmapSource();
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

    private static Mat? ResizeScribbleToSize(Mat? scribble, Size targetSize)
    {
        if (scribble is null)
        {
            return null;
        }
        var resized = new Mat();
        Cv2.Resize(scribble, resized, targetSize, interpolation: InterpolationFlags.Nearest);
        return resized;
    }

    // --- Export ---

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_workingBgr is null || _workingAlpha is null)
        {
            StatusMessage = "Run Apply before exporting.";
            return;
        }

        var suggested = _loadedImage is not null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath) + "_cutout.png"
            : "cutout.png";

        var path = _dialogs.ShowSavePngDialog(suggested);
        if (path is null)
        {
            return;
        }

        try
        {
            using var bgra = new Mat();
            Cv2.CvtColor(_workingBgr, bgra, ColorConversionCodes.BGR2BGRA);
            ReplaceAlphaChannel(bgra, _workingAlpha);

            switch (ExportBackgroundMode)
            {
                case ExportBackgroundMode.Transparent:
                    await _imageExporter.ExportPngAsync(bgra, path);
                    break;

                case ExportBackgroundMode.SolidColor:
                {
                    var colorBgr = new Vec3b(ExportSolidColor.B, ExportSolidColor.G, ExportSolidColor.R);
                    using var composited = BackgroundCompositingService.CompositeOntoColor(bgra, colorBgr);
                    await ExportBgrAsPngAsync(composited, path);
                    break;
                }

                case ExportBackgroundMode.Image:
                {
                    if (ExportBackgroundImagePath is null)
                    {
                        StatusMessage = "Choose a background image first.";
                        return;
                    }
                    using var background = await _imageLoader.LoadAsync(ExportBackgroundImagePath);
                    using var composited = BackgroundCompositingService.CompositeOntoImage(bgra, background.FullBgr);
                    await ExportBgrAsPngAsync(composited, path);
                    break;
                }
            }

            StatusMessage = $"Exported to {path}";
            _log.Info($"Exported to {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _log.Error("Export failed", ex);
        }
    }

    private async Task ExportBgrAsPngAsync(Mat bgr, string path)
    {
        using var bgra = new Mat();
        Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        await _imageExporter.ExportPngAsync(bgra, path);
    }

    [RelayCommand]
    private void PickBackgroundImage()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            ExportBackgroundImagePath = path;
        }
    }

    // --- Batch ---

    private bool CanBatch() => IsImageLoaded && !IsBusy && SelectedStrategy is not (StrategyKind.GrabCut or StrategyKind.Sam)
        && (SelectedStrategy != StrategyKind.Onnx || Onnx.IsModelReady);

    [RelayCommand(CanExecute = nameof(CanBatch))]
    private async Task BatchAsync()
    {
        if (!_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        var inputFolder = _dialogs.ShowOpenFolderDialog("Select folder with images to process");
        if (inputFolder is null)
        {
            return;
        }

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
        var files = Directory.EnumerateFiles(inputFolder)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (files.Count == 0)
        {
            StatusMessage = "No supported images found in that folder.";
            return;
        }

        string outputFolder = Path.Combine(inputFolder, "cutouts");
        var context = BuildContext();

        try
        {
            IsBatchRunning = true;
            var progress = new Progress<BatchProgress>(p =>
                BatchStatus = p.Completed >= p.Total ? "Batch complete." : $"Processing {p.Completed + 1}/{p.Total}: {Path.GetFileName(p.CurrentFile)}");
            await _batchProcessor.RunAsync(files, strategy, context, outputFolder, progress, CancellationToken.None);
            StatusMessage = $"Batch complete: {files.Count} image(s) exported to {outputFolder}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch failed: {ex.Message}";
            _log.Error("Batch failed", ex);
        }
        finally
        {
            IsBatchRunning = false;
        }
    }

    public void Dispose()
    {
        _loadedImage?.Dispose();
        _preview?.Dispose();
        _lastPreviewResult?.Dispose();
        DisposeWorkingResult();
        ClearScribbles();
        _editHistory.Dispose();
    }
}
