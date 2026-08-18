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
using BackgroundImageRemover.Services.Projects;
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
    private readonly IProjectService _projectService;
    private readonly IFileLogService _log;
    private readonly IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;
    private readonly EditHistory _editHistory = new();

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _processCts;

    private LoadedImage? _loadedImage;
    private PreviewImage? _preview;
    private RemovalResult? _lastPreviewResult;

    // The "working" composited result: BGR color (may include chroma-key spill correction) and
    // alpha kept apart so Undo/Redo, Brush and Magic Wand can mutate just the alpha cheaply.
    private Mat? _workingBgr;
    private Mat? _workingAlpha;

    // Whether the working result is authoritative (the live preview must not replace it on
    // the next export): true for loaded cutouts and for results the user has hand-edited.
    private bool _workingResultIsLoadedCutout;
    private bool _workingResultHandEdited;

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
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _title = "Untitled";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
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

    /// <summary>True when the opened file already carries an alpha channel (a previously cleaned cutout).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CutoutHint))]
    [NotifyPropertyChangedFor(nameof(CutoutStatus))]
    private bool _isCutout;

    /// <summary>Explains that a cutout is being refined, not re-cleaned from scratch.</summary>
    public string? CutoutHint => IsCutout
        ? "Already a cleaned cutout — you are refining it, not starting over."
        : null;

    /// <summary>Persistent status-bar hint shown while the open file is a clean cutout.</summary>
    public string CutoutStatus => "Clean cutout — refine with Brush/Wand or re-run a strategy.";

    /// <summary>True when the working result has changes not yet persisted with Save (Ctrl+S).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirtyHint))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private bool _isDirty;

    /// <summary>Explains the unsaved-work indicator in the tab header.</summary>
    public string? DirtyHint => IsDirty
        ? "Unsaved changes — press Ctrl+S (Save) to persist them."
        : null;

    /// <summary>Chrome title for the main window: document name plus a dirty marker.</summary>
    public string WindowTitle => Title + (IsDirty ? " *" : string.Empty) + " — Background Image Remover";

    /// <summary>Tab header title: document name plus a dirty asterisk.</summary>
    public string TabTitle => IsDirty ? Title + " *" : Title;

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

    /// <summary>The .ibrproj file this document was loaded from / last saved to; null until saved.</summary>
    [ObservableProperty]
    private string? _projectPath;

    /// <summary>The currently loaded source image, for tools outside the removal pipeline (e.g.
    /// the standalone Uncrop window) that need their own copy of it. Null until an image is loaded.</summary>
    public LoadedImage? LoadedImageForUncrop => _loadedImage;

    public bool HasProject => ProjectPath is not null;
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
        IProjectService projectService,
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
        _projectService = projectService;
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
                ExportCommand.NotifyCanExecuteChanged();
                ClearScribbles();
                if (GrabCut.HasValidRect)
                {
                    RequestPreviewDebounced();
                }
            }
            if (e.PropertyName == nameof(GrabCut.HasScribbles))
            {
                ExportCommand.NotifyCanExecuteChanged();
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
                ExportCommand.NotifyCanExecuteChanged();
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
            await LoadAsync(path);
        }
    }

    /// <summary>Loads an image or a <c>.ibrproj</c> project, dispatching on the file extension.</summary>
    public async Task LoadAsync(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".ibrproj", StringComparison.OrdinalIgnoreCase))
        {
            await LoadProjectAsync(path);
        }
        else
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
            _previewCts?.Cancel();

            _loadedImage?.Dispose();
            _preview?.Dispose();
            DisposeWorkingResult();
            _editHistory.Clear();
            RefreshUndoRedoState();
            _samEmbedding = null;
            _samPromptPointPreview = null;
            Sam.HasClickedPoint = false;

            _loadedImage = await _imageLoader.LoadAsync(path);
            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;

            // When reopening a saved cutout (an image with an alpha channel), show it with
            // its transparency in the Original pane too, instead of the flattened BGR — the
            // flattened version can look black where the removed content had dark RGB.
            bool isActualCutout = BackgroundCompositingService.HasMeaningfulTransparency(_loadedImage.FullAlpha);
            PreviewBitmap = isActualCutout
                ? BuildPreviewBitmapWithAlpha(preview, _loadedImage.FullAlpha!)
                : preview.Bgr.ToBitmapSource();
            ResultBitmap = null;
            IsImageLoaded = true;
            IsCutout = isActualCutout;
            Title = IsCutout ? Path.GetFileName(path) + " (cutout)" : Path.GetFileName(path);
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

            // A file with genuine transparency is a previously exported cutout, not a fresh
            // photo: adopt it as the working result so the user can keep refining it
            // (Brush/Wand, or re-run a strategy) instead of re-cleansing it from scratch.
            // A PNG that merely carries an (fully opaque) alpha channel is a plain photo and
            // must go through the normal removal flow instead of being adopted as-is.
            if (isActualCutout)
            {
                AdoptLoadedCutout();
            }
            else
            {
                RequestPreviewDebounced();
            }
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

    /// <summary>Builds a preview-resolution BGRA bitmap from the preview BGR plus a downscaled alpha channel.</summary>
    private static BitmapSource BuildPreviewBitmapWithAlpha(PreviewImage preview, Mat fullAlpha)
    {
        using var previewAlpha = new Mat();
        Cv2.Resize(fullAlpha, previewAlpha, preview.Bgr.Size(), interpolation: InterpolationFlags.Area);
        using var bgra = new Mat();
        Cv2.CvtColor(preview.Bgr, bgra, ColorConversionCodes.BGR2BGRA);
        BackgroundCompositingService.ReplaceAlphaChannel(bgra, previewAlpha);
        return bgra.ToBitmapSource();
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
            using var fgFull = ResizeScribbleToSize(_grabCutFgScribble, _loadedImage.FullBgr.Size());
            using var bgFull = ResizeScribbleToSize(_grabCutBgScribble, _loadedImage.FullBgr.Size());
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
        _workingResultHandEdited = true;
        IsDirty = true;
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
        _workingResultHandEdited = true;
        IsDirty = true;
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
        _workingResultHandEdited = true;
        IsDirty = true;
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
        _workingResultHandEdited = true;
        IsDirty = true;
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
            for (int i = MaxScribbleHistoryDepth - 1; i >= 0; i--) _scribbleUndo.Push(items[i]);
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

    private bool CanExport() => IsImageLoaded && !IsBusy
        && (HasWorkingResult || IsSelectedStrategyReady());

    private bool IsSelectedStrategyReady() => SelectedStrategy switch
    {
        StrategyKind.GrabCut => GrabCut.HasValidRect || GrabCut.HasScribbles,
        StrategyKind.Onnx => Onnx.IsModelReady,
        StrategyKind.Sam => Sam.IsModelReady && Sam.HasClickedPoint,
        _ => true
    };

    /// <summary>Exports the full-size cutout without cropping (transparent margins kept).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync() => ExportCoreAsync(crop: false);

    /// <summary>Exports the cutout trimmed to the subject (transparent borders removed).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCroppedAsync() => ExportCoreAsync(crop: true);

    /// <summary>
    /// The single "complete the job" action: computes the full-resolution cutout (faithful
    /// to the preview) when needed, then exports it, optionally trimming transparent borders.
    /// Loaded cutouts / hand-edited results are exported as-is.
    /// </summary>
    private async Task ExportCoreAsync(bool crop)
    {
        if (!await EnsureWorkingResultAsync() || _workingBgr is null || _workingAlpha is null)
        {
            return;
        }

        var baseName = _loadedImage is not null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath)
            : "cutout";
        var suggested = crop ? baseName + "_cropped.png" : baseName + "_cutout.png";

        var path = _dialogs.ShowSavePngDialog(suggested);
        if (path is null)
        {
            return;
        }

        try
        {
            using var bgra = new Mat();
            Cv2.CvtColor(_workingBgr, bgra, ColorConversionCodes.BGR2BGRA);
            BackgroundCompositingService.ReplaceAlphaChannel(bgra, _workingAlpha);

            // Fully-removed pixels must not carry the original color data forward: leaving it
            // in place is invisible today, but re-running a strategy (or reopening the file)
            // later reads it back as real image content and can resurrect the old background.
            BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);

            // "Crop" trims the transparent margins so the exported PNG hugs the subject.
            using var cropped = crop ? BackgroundCompositingService.TrimTransparentBorders(bgra) : null;
            var exportBgra = cropped ?? bgra;

            switch (ExportBackgroundMode)
            {
                case ExportBackgroundMode.Transparent:
                    await _imageExporter.ExportPngAsync(exportBgra, path);
                    break;

                case ExportBackgroundMode.SolidColor:
                {
                    var colorBgr = new Vec3b(ExportSolidColor.B, ExportSolidColor.G, ExportSolidColor.R);
                    using var composited = BackgroundCompositingService.CompositeOntoColor(exportBgra, colorBgr);
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
                    using var composited = BackgroundCompositingService.CompositeOntoImage(exportBgra, background.FullBgr);
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

    // --- Project (.ibrproj) ---

    private bool CanSaveProject() => IsImageLoaded && !IsBusy;

    /// <summary>Saves the document to its current project path, or asks for one on first save.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsync()
    {
        if (ProjectPath is null)
        {
            await SaveProjectAsAsync();
        }
        else
        {
            await SaveProjectToPathAsync(ProjectPath);
        }
    }

    /// <summary>Saves the project (prompting for a path on first save); returns true when persisted.</summary>
    public async Task<bool> TrySaveProjectAsync()
    {
        await SaveProjectAsync();
        return !IsDirty;
    }

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsAsync()
    {
        if (_loadedImage is null)
        {
            return;
        }
        var baseName = ProjectPath is null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath)
            : Path.GetFileNameWithoutExtension(ProjectPath);
        var path = _dialogs.ShowSaveProjectDialog(baseName + ".ibrproj");
        if (path is null)
        {
            return;
        }
        await SaveProjectToPathAsync(path);
    }

    private async Task SaveProjectToPathAsync(string path)
    {
        if (_loadedImage is null)
        {
            return;
        }

        try
        {
            var settings = BuildProjectDocument();
            await _projectService.SaveAsync(
                path,
                _loadedImage.FullBgr,
                _loadedImage.FullAlpha,
                _workingBgr,
                _workingAlpha,
                settings);

            ProjectPath = path;
            Title = Path.GetFileName(path);
            IsDirty = false; // the working result is now persisted inside the project
            StatusMessage = $"Project saved to {path}";
            _log.Info($"Project saved to {path}");
            _settings.AddRecentProject(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save project failed: {ex.Message}";
            _log.Error("Save project failed", ex);
        }
    }

    /// <summary>Captures all editable settings into a persistable snapshot.</summary>
    private ProjectDocument BuildProjectDocument()
    {
        return new ProjectDocument
        {
            Title = Title,
            SelectedStrategy = SelectedStrategy.ToString(),
            ChromaKeyTolerance = ChromaKey.Tolerance,
            ChromaKeySpillSuppression = ChromaKey.SpillSuppression,
            ChromaKeyDetectedColorBgr = ChromaKey.DetectedColorBgr is { } c
                ? new[] { c.Item0, c.Item1, c.Item2 }
                : null,
            OnnxModel = Onnx.SelectedModel.ToString(),
            OnnxFeatherPixels = Onnx.FeatherPixels,
            OnnxEnableAlphaMatting = Onnx.EnableAlphaMatting,
            ExportBackgroundMode = ExportBackgroundMode.ToString(),
            ExportSolidColor = ExportSolidColor.ToString(),
            ExportBackgroundImagePath = ExportBackgroundImagePath,
            BrushRadius = BrushRadius,
            BrushHardness = BrushHardness,
            BrushMode = BrushMode.ToString(),
            MagicWandTolerance = MagicWandTolerance,
            GrabCutRect = GrabCut.SelectedRect is { } rect ? new[] { rect.X, rect.Y, rect.Width, rect.Height } : null,
            SamPoint = _samPromptPointPreview is { } p ? new[] { (int)Math.Round(p.X), (int)Math.Round(p.Y) } : null
        };
    }

    /// <summary>Restores settings from a loaded project (tolerant of unknown enum names).</summary>
    private void ApplyProjectDocument(ProjectDocument p)
    {
        if (Enum.TryParse<StrategyKind>(p.SelectedStrategy, out var strategy))
        {
            SelectedStrategy = strategy;
        }

        ChromaKey.Tolerance = p.ChromaKeyTolerance;
        ChromaKey.SpillSuppression = p.ChromaKeySpillSuppression;
        if (p.ChromaKeyDetectedColorBgr is { Length: 3 } bgr)
        {
            ChromaKey.DetectedColorBgr = new Vec3b(bgr[0], bgr[1], bgr[2]);
        }

        if (Enum.TryParse<OnnxModelKind>(p.OnnxModel, out var model))
        {
            Onnx.SelectedModel = model;
        }
        Onnx.FeatherPixels = p.OnnxFeatherPixels;
        Onnx.EnableAlphaMatting = p.OnnxEnableAlphaMatting;

        if (Enum.TryParse<ExportBackgroundMode>(p.ExportBackgroundMode, out var exportMode))
        {
            ExportBackgroundMode = exportMode;
        }
        if (!string.IsNullOrWhiteSpace(p.ExportSolidColor)
            && System.Windows.Media.ColorConverter.ConvertFromString(p.ExportSolidColor) is WpfColor solid)
        {
            ExportSolidColor = solid;
        }
        ExportBackgroundImagePath = p.ExportBackgroundImagePath;

        BrushRadius = p.BrushRadius;
        BrushHardness = p.BrushHardness;
        if (Enum.TryParse<BrushMode>(p.BrushMode, out var brushMode))
        {
            BrushMode = brushMode;
        }
        MagicWandTolerance = p.MagicWandTolerance;

        if (p.GrabCutRect is { Length: 4 } rect && rect[2] > 0 && rect[3] > 0)
        {
            GrabCut.SelectedRect = new Rect(rect[0], rect[1], rect[2], rect[3]);
        }

        if (p.SamPoint is { Length: 2 } sam)
        {
            _samPromptPointPreview = new WpfPoint(sam[0], sam[1]);
            Sam.HasClickedPoint = true;
        }
    }

    /// <summary>Loads a <c>.ibrproj</c> project into this document, replacing the current state.</summary>
    public async Task LoadProjectAsync(string path)
    {
        LoadedProject? loaded = null;
        PreviewImage? preview = null;
        var adopted = false;

        try
        {
            IsBusy = true;
            BusyMessage = "Loading project...";
            _previewCts?.Cancel();

            _loadedImage?.Dispose();
            _preview?.Dispose();
            DisposeWorkingResult();
            _editHistory.Clear();
            RefreshUndoRedoState();
            _samEmbedding = null;
            _samPromptPointPreview = null;
            Sam.HasClickedPoint = false;
            ProjectPath = null;
            GrabCut.SelectedRect = null;

            loaded = await _projectService.LoadAsync(path);
            preview = _downscaler.CreatePreview(loaded.OriginalBgr);

            var previewBitmap = loaded.OriginalAlpha is not null
                ? BuildPreviewBitmapWithAlpha(preview, loaded.OriginalAlpha)
                : preview.Bgr.ToBitmapSource();

            // Restore settings while IsImageLoaded is still false so strategy-change handlers
            // don't kick off spurious previews; the saved working result is authoritative.
            ApplyProjectDocument(loaded.Settings);

            // Everything decoded and applied — adopt the Mats (ownership transfers here).
            _loadedImage = new LoadedImage(path, loaded.OriginalBgr, loaded.OriginalAlpha);
            _preview = preview;
            PreviewBitmap = previewBitmap;
            ResultBitmap = null;

            ProjectPath = path;

            if (loaded.WorkingBgr is not null && loaded.WorkingAlpha is not null)
            {
                _workingBgr = loaded.WorkingBgr;
                _workingAlpha = loaded.WorkingAlpha;
                _workingResultIsLoadedCutout = true;
                _workingResultHandEdited = false;
                OnPropertyChanged(nameof(HasWorkingResult));
                RefreshUndoRedoState();
                ExportCommand.NotifyCanExecuteChanged();
                IsDirty = false;
                RefreshResultBitmapFromWorking();
            }

            adopted = true;
            loaded = null;  // Mats now owned by the document
            preview = null; // Preview now owned by the document

            IsImageLoaded = true;
            IsCutout = _workingAlpha is not null;
            Title = Path.GetFileName(path);
            StatusMessage = $"Loaded project {Path.GetFileName(path)} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
            _log.Info($"Loaded project {path}");
            _settings.AddRecentProject(path);

            ClearScribbles();
            if (ChromaKey.DetectedColorBgr is null)
            {
                ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);
            }

            if (SelectedStrategy == StrategyKind.Sam && Sam.IsModelReady)
            {
                ComputeSamEmbedding();
            }

            if (_workingAlpha is null)
            {
                RequestPreviewDebounced();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load project: {ex.Message}";
            _log.Error($"Could not load project: {path}", ex);
        }
        finally
        {
            if (!adopted)
            {
                loaded?.Dispose();
                preview?.Dispose();
            }
            IsBusy = false;
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
