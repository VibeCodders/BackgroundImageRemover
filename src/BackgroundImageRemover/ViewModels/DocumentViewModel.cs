using System.Collections.ObjectModel;
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
using ImageLayer = BackgroundImageRemover.Models.ImageLayer;
using Channel = BackgroundImageRemover.Models.Channel;
using PathObject = BackgroundImageRemover.Models.PathObject;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// State and core lifecycle logic for a single open image ("document"): one per tab.
/// Owns collections and coordinates tool operations.
/// </summary>
public partial class DocumentViewModel : ObservableObject, IDocumentTab, IDisposable, IStrategyParameterSource
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
    private readonly DocumentEditHistory _history = new();

    private readonly PreviewRunner _previews;
    private CancellationTokenSource? _processCts;
    private CancellationTokenSource? _uncropCts;

    private LoadedImage? _loadedImage;
    private PreviewImage? _preview;

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
    private readonly BrushStrokeController _strokes = new();
    private readonly ModelManager _models;

    private SamEmbedding? _samEmbedding;
    private WpfPoint? _samPromptPointPreview;
    private List<WpfPoint>? _samPromptPointsPreview;
    private WpfPoint? _magicWandSeedPreview;

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();
    public SamStrategyViewModel Sam { get; } = new();
    public FloodFillStrategyViewModel FloodFill { get; } = new();
    public KMeansStrategyViewModel KMeans { get; } = new();
    public MagicWandStrategyViewModel MagicWand { get; } = new();
    public InpaintStrategyViewModel Inpaint { get; } = new();

    private ShellViewModel? _shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveToolSession))]
    private IToolSessionTab? _activeToolSession;

    public bool HasActiveToolSession => ActiveToolSession is not null;

    /// <summary>The tool palette, grouped for display -- <see cref="Views.Controls.StrategyToolbar"/>
    /// binds to this instead of hand-listing every tool/strategy icon.</summary>
    public IReadOnlyList<Tools.ToolCategory> ToolCategories => _shell?.ToolCategories ?? Array.Empty<Tools.ToolCategory>();

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
    /// Opens the Background Remover tool session pre-selected to the given strategy. Each
    /// background-removal strategy has its own icon in the left toolbar, GIMP-style.
    /// </summary>
    [RelayCommand]
    public void OpenBackgroundRemovalTool(StrategyKind strategy)
    {
        if (!IsImageLoaded || _shell is null) return;
        _shell.OpenToolSession(this, EditorTool.RemoveBackground, strategy);
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

        RecordCurrentStateForUndo(operationName);
        ReplaceWorkingState(newBgr, newAlpha);
        StatusMessage = $"Applied {operationName}.";
    }

    /// <summary>Snapshots the current working result (or the loaded image when no result exists yet).</summary>
    private void RecordCurrentStateForUndo(string operationName)
    {
        if (_workingBgr is not null && _workingAlpha is not null)
        {
            _history.Record(operationName, _workingBgr, _workingAlpha);
            return;
        }

        if (_loadedImage is not null)
        {
            if (_loadedImage.FullAlpha is { } alpha)
            {
                _history.Record(operationName, _loadedImage.FullBgr, alpha);
            }
            else
            {
                using var opaque = new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
                _history.Record(operationName, _loadedImage.FullBgr, opaque);
            }
        }
    }

    /// <summary>Rebuilds the observable timeline from the undo/redo history.</summary>
    private void RefreshEditSteps()
    {
        EditSteps.Clear();
        foreach (var step in _history.BuildSteps())
        {
            EditSteps.Add(step);
        }
        OnPropertyChanged(nameof(HasEditSteps));
    }

    /// <summary>Replaces the working BGR/alpha pair, rebuilding the loaded image and preview when the size changed.</summary>
    private void ReplaceWorkingState(Mat newBgr, Mat newAlpha)
    {
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
        _workingBgr = newBgr;
        _workingAlpha = newAlpha;
        _workingResultIsLoadedCutout = false;
        _workingResultHandEdited = true;

        EnsureLoadedImageMatchesWorkingSize();

        FinalizeWorkingState(markDirty: true);
    }

    /// <summary>Rebuilds the loaded image (and preview) from the working result when its size differs.</summary>
    private void EnsureLoadedImageMatchesWorkingSize()
    {
        if (_loadedImage is null || _workingBgr is null || _workingAlpha is null)
        {
            return;
        }
        if (_loadedImage.FullBgr.Size() == _workingBgr.Size())
        {
            return;
        }

        var newLoaded = new LoadedImage(_loadedImage.FilePath, _workingBgr.Clone(), _workingAlpha.Clone());
        _loadedImage.Dispose();
        _preview?.Dispose();
        _loadedImage = newLoaded;

        var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
        _preview = preview;
        PreviewBitmap = preview.Bgr.ToBitmapSource();

        // A size-changing edit (crop, resize, transform, frame, compose...) rebuilt the
        // source image above: keep the status-bar dimensions in sync or they go stale.
        ImageWidth = _loadedImage.FullBgr.Width;
        ImageHeight = _loadedImage.FullBgr.Height;
        OnPropertyChanged(nameof(ImageDimensions));
    }

    /// <summary>Finalizes the UI state after an undo/redo restored a different working result.</summary>
    private void FinalizeHistoryRestore(string status)
    {
        _workingResultIsLoadedCutout = false;
        _workingResultHandEdited = true;
        EnsureLoadedImageMatchesWorkingSize();
        FinalizeWorkingState(markDirty: true, status: status);
    }

    /// <summary>
    /// Shared "working state changed" ceremony: recomputes the cutout flag, refreshes the
    /// observable undo/redo availability, re-renders the result bitmap and notifies the display.
    /// Callers differ only in the dirty flag, whether the export/has-result command availability
    /// must be re-notified (strategy results and adopted cutouts re-enable Export; plain edits and
    /// history restores re-evaluate it through RefreshUndoRedoState only) and an optional status line.
    /// </summary>
    private void FinalizeWorkingState(bool markDirty, bool notifyCommandAvailability = false, string? status = null)
    {
        IsDirty = markDirty;
        IsCutout = BackgroundCompositingService.HasMeaningfulTransparency(_workingAlpha);
        RefreshUndoRedoState();
        if (notifyCommandAvailability)
        {
            OnPropertyChanged(nameof(HasWorkingResult));
            ExportCommand.NotifyCanExecuteChanged();
        }
        RefreshResultBitmapFromWorking();
        OnPropertyChanged(nameof(DisplayBitmap));
        if (status is not null)
        {
            StatusMessage = status;
        }
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

    [ObservableProperty]
    private bool _invertMask;

    [ObservableProperty]
    private double _maskFeatherPixels;

    [ObservableProperty]
    private int _maskExpandPixels;

    [ObservableProperty]
    private double _maskBlurPixels;

    [ObservableProperty]
    private int _minComponentAreaPixels;

    [ObservableProperty]
    private double _maskGamma = 1.0;

    [ObservableProperty]
    private double _maskHardness;

    [ObservableProperty]
    private int _maskThreshold;

    [ObservableProperty]
    private double _despillStrength = 1.0;

    [ObservableProperty]
    private int _maskMedianKernel;

    [ObservableProperty]
    private int _maskBilateralKernel;

    [ObservableProperty]
    private bool _maskClahe;

    // The inline canvas preview (GIMP-style, single-click-select) has no UI for these extra
    // cleanup passes -- only the dedicated Background Remover tool tab does. Reporting the
    // StrategyContext record's own no-op defaults here keeps IStrategyParameterSource honest
    // without adding controls this view doesn't have.
    int IStrategyParameterSource.DespeckleKernelSize => 0;
    int IStrategyParameterSource.FillHolesKernelSize => 0;
    int IStrategyParameterSource.SmoothEdgesKernelSize => 0;
    bool IStrategyParameterSource.KeepLargestComponent => false;

    /// <summary>
    /// The active display bitmap: shows the processed ResultBitmap if available,
    /// otherwise falls back to the clean loaded PreviewBitmap.
    /// </summary>
    public BitmapSource? DisplayBitmap => ResultBitmap ?? PreviewBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyUncropCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CwCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CcwCommand))]
    private bool _isImageLoaded;

    private readonly BusyGate _busyGate = new();

    /// <summary>
    /// True while a background run is in flight. Commands that must not run while busy are
    /// routed through <see cref="BusyGate.Gate"/> (see the UndoCommand/ExportCommand/...
    /// properties) and are disabled and re-evaluated automatically on every flip — nothing
    /// else to wire up. Raised as a property change so the busy overlay follows it.
    /// </summary>
    public bool IsBusy
    {
        get => _busyGate.IsBusy;
        set => _busyGate.SetBusy(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageDimensions))]
    private int _imageWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageDimensions))]
    private int _imageHeight;

    /// <summary>Current image dimensions for the status bar, e.g. "1920 × 1080".</summary>
    public string ImageDimensions => IsImageLoaded && ImageWidth > 0 && ImageHeight > 0
        ? $"{ImageWidth} × {ImageHeight}"
        : string.Empty;

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

    /// <summary>Chronological undo/redo timeline for the history panel.</summary>
    public ObservableCollection<EditHistoryStep> EditSteps { get; } = new();

    public bool HasEditSteps => EditSteps.Count > 0;

    /// <summary>Layers collection for the Layers panel (GIMP-style).</summary>
    public ObservableCollection<ImageLayer> Layers { get; } = new();

    /// <summary>Channels collection for the Channels panel (GIMP-style).</summary>
    public ObservableCollection<Channel> Channels { get; } = new();

    /// <summary>Paths collection for the Paths panel (GIMP-style).</summary>
    public ObservableCollection<PathObject> Paths { get; } = new();

    /// <summary>Step clicked in the history panel; restored immediately then reset to null.</summary>
    [ObservableProperty]
    private EditHistoryStep? _selectedEditStep;

    partial void OnSelectedEditStepChanged(EditHistoryStep? value)
    {
        if (value is null)
        {
            return;
        }
        RestoreToEditStep(value);
        SelectedEditStep = null;
    }

    /// <summary>Jumps the working state to the clicked timeline step (undo/redo as needed).</summary>
    private void RestoreToEditStep(EditHistoryStep step)
    {
        if (_loadedImage is null)
        {
            return;
        }

        int index = EditSteps.IndexOf(step);
        if (index < 0)
        {
            return;
        }

        if (!_history.RestoreTo(index, ref _workingBgr, ref _workingAlpha, out var name))
        {
            return;
        }
        FinalizeHistoryRestore(string.IsNullOrEmpty(name) ? "Restored history state." : $"Restored: {name}");
    }

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

        _models = new ModelManager(
            _onnxStrategy,
            _samStrategy,
            _log,
            () => _loadedImage?.FullBgr,
            error => StatusMessage = $"SAM embedding failed: {error}",
            RequestPreviewDebounced,
            () => _samEmbedding is not null,
            () => StatusMessage = "SAM is still preparing this image, try again in a moment.");

        _useGpuForOnnx = settings.Current.UseGpuForOnnx;
        _onnxStrategy.SetUseGpu(_useGpuForOnnx);

        // The busy flag drives the undo/redo availability (they would dispose the live
        // working Mats while a run is in flight) and must raise PropertyChanged for the
        // busy overlay binding.
        _busyGate.BusyChanged += value =>
        {
            OnPropertyChanged(nameof(IsBusy));
            RefreshUndoRedoState();
        };

        // Cancel-style commands must stay enabled while busy: they are re-evaluated on every
        // busy flip (their CanExecute is "busy AND something") without being gated.
        _busyGate.Track(CancelUncropCommand);

        _previews = new PreviewRunner(
            () => _preview,
            _strategies,
            () => SelectedStrategy,
            IsPreviewReady,
            () => ScribbleManager,
            (fg, bg) => BuildContext(grabCutFg: fg, grabCutBg: bg),
            bitmap => ResultBitmap = bitmap,
            message => StatusMessage = message,
            () => { },
            () => IsImageLoaded && ResultMode == InteractionMode.None);

        // Subscribe to scribble manager events so the overlay stays in sync with the masks.
        _scribbleManager.StrokeUndone += (_, _) => RefreshScribbleOverlay();
        _scribbleManager.StrokeRedone += (_, _) => RefreshScribbleOverlay();
        _scribbleManager.ScribblesCleared += (_, _) => RefreshScribbleOverlay();

        // Keep the history panel in sync with undo/redo timeline changes.
        _history.Changed += (_, _) => RefreshEditSteps();

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

        MagicWand.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MagicWand.Tolerance))
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

        // Carry the last session's export setup into this document so a configured brand
        // background (color/gradient/shadow/quality) does not reset on every new tab.
        RestoreExportSettings();
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

    partial void OnInvertMaskChanged(bool value) => RequestPreviewDebounced();

    partial void OnMaskFeatherPixelsChanged(double value) => RequestPreviewDebounced();

    partial void OnMaskExpandPixelsChanged(int value) => RequestPreviewDebounced();

    partial void OnMaskBlurPixelsChanged(double value) => RequestPreviewDebounced();

    partial void OnMinComponentAreaPixelsChanged(int value) => RequestPreviewDebounced();

    partial void OnMaskGammaChanged(double value) => RequestPreviewDebounced();

    partial void OnMaskHardnessChanged(double value) => RequestPreviewDebounced();

    partial void OnMaskThresholdChanged(int value) => RequestPreviewDebounced();

    partial void OnDespillStrengthChanged(double value) => RequestPreviewDebounced();

    partial void OnMaskMedianKernelChanged(int value) => RequestPreviewDebounced();

    partial void OnMaskBilateralKernelChanged(int value) => RequestPreviewDebounced();

    partial void OnMaskClaheChanged(bool value) => RequestPreviewDebounced();

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
            StrategyKind.MagicWand => InteractionMode.MagicWand,
            _ => InteractionMode.None
        };

        if (value == StrategyKind.Inpaint)
        {
            // Inpaint has no click-to-seed interaction: it floods from the image border, so
            // there is no special interaction mode to enter.
            OriginalMode = InteractionMode.None;
        }

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
        // Stop pending work so no background task keeps touching Mats after the tab closes.
        _previews.Dispose();
        _processCts?.Cancel();
        _processCts?.Dispose();
        _brushRefreshTimer?.Stop();
        _uncropCts?.Cancel();
        _uncropCts?.Dispose();
        _loadedImage?.Dispose();
        _preview?.Dispose();
        DisposeWorkingResult();
        ScribbleManager.Dispose();
        _history.Dispose();
    }
}
