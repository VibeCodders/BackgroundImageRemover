using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
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
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// State and core lifecycle logic for a single open image ("document"): one per tab.
/// Owns collections and coordinates tool operations.
/// </summary>
public partial class DocumentViewModel : ObservableObject, IDocumentTab, IDisposable
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
    private readonly IUncropFillService _uncropFillService;
    private readonly MatEditSession _editSession = new();

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _processCts;
    private CancellationTokenSource? _uncropCts;

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

    private readonly ScribbleManager _scribbleManager = new();
    internal ScribbleManager ScribbleManager => _scribbleManager; // Expose for partial classes
    private WpfPoint? _brushLastPoint;

    private SamEmbedding? _samEmbedding;
    private WpfPoint? _samPromptPointPreview;

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();
    public SamStrategyViewModel Sam { get; } = new();
    public FloodFillStrategyViewModel FloodFill { get; } = new();
    public KMeansStrategyViewModel KMeans { get; } = new();

    private ShellViewModel? _shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveToolSession))]
    private IToolSessionTab? _activeToolSession;

    public bool HasActiveToolSession => ActiveToolSession is not null;

    public void SetShell(ShellViewModel shell) => _shell = shell;

    /// <summary>
    /// Opens the specified tool in a dedicated modal tab.
    /// </summary>
    [RelayCommand]
    public void OpenToolTab(EditorTool tool)
    {
        if (!IsImageLoaded || _shell is null) return;
        _shell.OpenToolSession(this, tool);
    }

    /// <summary>
    /// Creates a self-contained LoadedImage snapshot of the current state of this document
    /// (the working result if available, or the source image).
    /// </summary>
    public LoadedImage CreateCurrentStateSnapshot()
    {
        if (_workingBgr is not null && _workingAlpha is not null)
        {
            return new LoadedImage(_loadedImage?.FilePath ?? "Image", _workingBgr.Clone(), _workingAlpha.Clone());
        }
        if (_loadedImage is not null)
        {
            return _loadedImage.Clone();
        }
        throw new InvalidOperationException("No image is currently loaded.");
    }

    /// <summary>
    /// Applies the result returned by a dedicated tool tab back into this document,
    /// recording it into the Undo history.
    /// </summary>
    public void ApplyToolResult(Mat newBgr, Mat newAlpha, string operationName = "Edit")
    {
        if (_loadedImage is null) return;

        // Record previous alpha state to the undo stack before replacing
        if (_workingAlpha is not null)
        {
            _editSession.Record(_workingAlpha);
        }

        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
        _workingBgr = newBgr;
        _workingAlpha = newAlpha;
        _workingResultIsLoadedCutout = false;
        _workingResultHandEdited = true;

        // If dimensions changed (e.g. from Uncrop), reinitialize loaded image size and preview.
        // The original pane (PreviewBitmap) is only rebuilt here, so it keeps representing the
        // "before" state for the compare/split views instead of being overwritten with the
        // working alpha after every retouch or background-removal apply.
        if (_loadedImage.FullBgr.Size() != newBgr.Size())
        {
            var newLoaded = new LoadedImage(_loadedImage.FilePath, newBgr.Clone(), newAlpha.Clone());
            _loadedImage.Dispose();
            _preview?.Dispose();
            _loadedImage = newLoaded;

            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;
            PreviewBitmap = preview.Bgr.ToBitmapSource();
        }

        IsDirty = true;
        IsCutout = BackgroundCompositingService.HasMeaningfulTransparency(_workingAlpha);
        RefreshUndoRedoState();
        RefreshResultBitmapFromWorking();
        OnPropertyChanged(nameof(DisplayBitmap));
        StatusMessage = $"Applied {operationName}.";
    }

    [ObservableProperty]
    private EditorTool _activeTool = EditorTool.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _title = "Untitled";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayBitmap))]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayBitmap))]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private BitmapSource? _scribbleOverlay;

    /// <summary>
    /// The active display bitmap: shows the processed ResultBitmap if available,
    /// otherwise falls back to the clean loaded PreviewBitmap.
    /// </summary>
    public BitmapSource? DisplayBitmap => ResultBitmap ?? PreviewBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyUncropCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyUncropCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelUncropCommand))]
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

    /// <summary>
    /// When true, displays the side-by-side split view (Original on left, Result on right).
    /// When false (default), displays a single unified work area.
    /// </summary>
    [ObservableProperty]
    private bool _isSplitViewEnabled;

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
    private double _compareDividerPosition = 0.5;

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
        SamStrategy samStrategy,
        IUncropFillService uncropFillService)
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
        _uncropFillService = uncropFillService;

        _useGpuForOnnx = settings.Current.UseGpuForOnnx;
        _onnxStrategy.SetUseGpu(_useGpuForOnnx);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = RunPreviewAsync();
        };

        // Subscribe to scribble manager events so the overlay stays in sync with the masks.
        _scribbleManager.StrokeUndone += (_, _) => RefreshScribbleOverlay();
        _scribbleManager.StrokeRedone += (_, _) => RefreshScribbleOverlay();
        _scribbleManager.ScribblesCleared += (_, _) => RefreshScribbleOverlay();

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
                ScribbleManager.Clear();
                GrabCut.HasScribbles = false;
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

        FloodFill.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloodFill.Tolerance))
            {
                RequestPreviewDebounced();
            }
        };

        KMeans.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(KMeans.ClusterCount))
            {
                RequestPreviewDebounced();
            }
        };

        UncropOptions.ImageSizeProvider = () => _loadedImage?.FullBgr.Size();
        UncropOptions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UncropOptionsViewModel.Padding) or nameof(UncropOptionsViewModel.SelectedFillMode))
            {
                ApplyUncropCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnActiveToolChanged(EditorTool value)
    {
        switch (value)
        {
            case EditorTool.RemoveBackground:
                ResultMode = InteractionMode.None;
                OriginalMode = SelectedStrategy switch
                {
                    StrategyKind.GrabCut => InteractionMode.DrawRect,
                    StrategyKind.Sam => InteractionMode.SamClick,
                    _ => InteractionMode.None
                };
                break;
            case EditorTool.Retouch:
                OriginalMode = InteractionMode.None;
                if (ResultMode == InteractionMode.None)
                {
                    ResultMode = InteractionMode.Brush;
                }
                break;
            case EditorTool.Uncrop:
            case EditorTool.None:
            default:
                OriginalMode = InteractionMode.None;
                ResultMode = InteractionMode.None;
                break;
        }
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

    public void Dispose()
    {
        _brushRefreshTimer?.Stop();
        _uncropCts?.Cancel();
        _uncropCts?.Dispose();
        _loadedImage?.Dispose();
        _preview?.Dispose();
        _lastPreviewResult?.Dispose();
        DisposeWorkingResult();
        ScribbleManager.Dispose();
        _editSession.Dispose();
    }
}
