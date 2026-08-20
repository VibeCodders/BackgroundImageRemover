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
    private List<WpfPoint>? _samPromptPointsPreview;
    private WpfPoint? _magicWandSeedPreview;

    public override string ToolBadge => "✂ Background Remover";
    public override string AccentColor => "#1E7A33";

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();
    public SamStrategyViewModel Sam { get; } = new();
    public FloodFillStrategyViewModel FloodFill { get; } = new();
    public KMeansStrategyViewModel KMeans { get; } = new();
    public MagicWandStrategyViewModel MagicWand { get; } = new();
    public InpaintStrategyViewModel Inpaint { get; } = new();

    [ObservableProperty]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private BitmapSource? _scribbleOverlay;

    [ObservableProperty]
    private bool _invertMask;

    [ObservableProperty]
    private double _maskFeatherPixels;

    [ObservableProperty]
    private int _despeckleKernelSize = 0;

    [ObservableProperty]
    private int _fillHolesKernelSize = 0;

    [ObservableProperty]
    private int _smoothEdgesKernelSize = 0;

    [ObservableProperty]
    private bool _keepLargestComponent;

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

    [ObservableProperty]
    private bool _sampleColorMode;

    private readonly BusyGate _busyGate = new();

    /// <summary>True while a preview/apply is in flight; raises PropertyChanged for the busy
    /// overlay and re-evaluates the tracked commands on every flip.</summary>
    public bool IsBusy
    {
        get => _busyGate.IsBusy;
        set => _busyGate.SetBusy(value);
    }

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private InteractionMode _originalMode = InteractionMode.None;

    [ObservableProperty]
    private bool _useGpuForOnnx;

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

        // The busy overlay binds IsBusy, and Apply is tracked so its (re)evaluation follows
        // the busy flag like the generated NotifyCanExecuteChangedFor used to.
        _busyGate.BusyChanged += value => OnPropertyChanged(nameof(IsBusy));
        _busyGate.Track(ApplyCommand);

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await RunPreviewAsync();
        };

        // Subscribe to scribble manager events so the overlay stays in sync with the masks.
        ScribbleManager.StrokeUndone += (_, _) => RefreshScribbleOverlay();
        ScribbleManager.StrokeRedone += (_, _) => RefreshScribbleOverlay();
        ScribbleManager.ScribblesCleared += (_, _) => RefreshScribbleOverlay();

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

        // Start with no effect: a preview runs only after the user picks a strategy or tweaks a control.
    }

    partial void OnSelectedStrategyChanged(StrategyKind value)
    {
        // When "sample color by clicking" is on, the click mode belongs to the color sampler:
        // switching strategy must not silently drop it (the checkbox would stay checked while
        // clicks stopped sampling).
        OriginalMode = SampleColorMode
            ? InteractionMode.MagicWand
            : value switch
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

    partial void OnInvertMaskChanged(bool value) => RequestPreviewDebounced();

    partial void OnMaskFeatherPixelsChanged(double value) => RequestPreviewDebounced();

    partial void OnDespeckleKernelSizeChanged(int value) => RequestPreviewDebounced();

    partial void OnFillHolesKernelSizeChanged(int value) => RequestPreviewDebounced();

    partial void OnSmoothEdgesKernelSizeChanged(int value) => RequestPreviewDebounced();

    partial void OnKeepLargestComponentChanged(bool value) => RequestPreviewDebounced();

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

    partial void OnSampleColorModeChanged(bool value)
    {
        OriginalMode = value
            ? InteractionMode.MagicWand
            : SelectedStrategy switch
            {
                StrategyKind.GrabCut => InteractionMode.DrawRect,
                StrategyKind.Sam => InteractionMode.SamClick,
                StrategyKind.MagicWand => InteractionMode.MagicWand,
                _ => InteractionMode.None
            };
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
