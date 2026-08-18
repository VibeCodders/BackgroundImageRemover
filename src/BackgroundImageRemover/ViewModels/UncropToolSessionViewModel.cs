using System.IO;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Outpaint;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for Uncrop / Canvas Expansion outpainting.
/// </summary>
public partial class UncropToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IUncropFillService _fillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly EditHistory _editHistory = new();
    private CancellationTokenSource? _fillCts;

    private LoadedImage? _sourceImage;
    private Mat? _resultBgra;

    public override string ToolBadge => "⤢ Uncrop";
    public override string AccentColor => "#0078D7";

    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;
    public IReadOnlyList<UncropInpaintMethod> InpaintMethods { get; } = Enum.GetValues<UncropInpaintMethod>();
    public IReadOnlyList<UncropMirrorType> MirrorTypes { get; } = Enum.GetValues<UncropMirrorType>();
    public IReadOnlyList<UncropColorSource> ColorSources { get; } = Enum.GetValues<UncropColorSource>();
    public IReadOnlyList<UncropGradientMode> GradientModes { get; } = Enum.GetValues<UncropGradientMode>();

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private CanvasPadding _padding = CanvasPadding.Zero;

    [ObservableProperty]
    private UncropAspectPreset _selectedPreset = UncropAspectPresets.Free;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private UncropFillMode _selectedFillMode = UncropFillMode.Mirror;

    [ObservableProperty]
    private UncropMirrorType _selectedMirrorType = UncropMirrorType.Reflect101;

    [ObservableProperty]
    private int _mirrorBlurRadius = 0;

    [ObservableProperty]
    private double _mirrorFadeOpacity = 1.0;

    [ObservableProperty]
    private UncropInpaintMethod _selectedInpaintMethod = UncropInpaintMethod.Telea;

    [ObservableProperty]
    private double _inpaintRadius = 5.0;

    [ObservableProperty]
    private int _blendMargin = 0;

    [ObservableProperty]
    private bool _inpaintPreFillEdgeAverage;

    [ObservableProperty]
    private UncropColorSource _selectedColorSource = UncropColorSource.EdgeAverage;

    [ObservableProperty]
    private WpfColor _customSolidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _blurredColorFill;

    [ObservableProperty]
    private int _blurRadius = 0;

    [ObservableProperty]
    private int _replicateSmoothRadius = 0;

    [ObservableProperty]
    private int _zoomBlurRadius = 35;

    [ObservableProperty]
    private double _zoomScale = 1.25;

    [ObservableProperty]
    private UncropGradientMode _selectedGradientMode = UncropGradientMode.PerEdgeSplay;

    [ObservableProperty]
    private double _gradientNoiseAmount = 0.0;

    [ObservableProperty]
    private int _patchSize = 32;

    [ObservableProperty]
    private int _patchBlendOverlap = 8;

    [ObservableProperty]
    private BitmapSource? _previewResult;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    public int PaddingLeftPx
    {
        get => Padding.Left;
        set => SetPaddingFromUser(Padding with { Left = Math.Max(0, value) });
    }

    public int PaddingTopPx
    {
        get => Padding.Top;
        set => SetPaddingFromUser(Padding with { Top = Math.Max(0, value) });
    }

    public int PaddingRightPx
    {
        get => Padding.Right;
        set => SetPaddingFromUser(Padding with { Right = Math.Max(0, value) });
    }

    public int PaddingBottomPx
    {
        get => Padding.Bottom;
        set => SetPaddingFromUser(Padding with { Bottom = Math.Max(0, value) });
    }

    public UncropToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IUncropFillService fillService,
        IDialogService dialogs,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IFileLogService log)
        : base(shell, parentDocument)
    {
        _fillService = fillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;

        var sourceSnapshot = parentDocument.CreateCurrentStateSnapshot();
        AdoptImage(sourceSnapshot);
    }

    partial void OnPaddingChanged(CanvasPadding value)
    {
        OnPropertyChanged(nameof(PaddingLeftPx));
        OnPropertyChanged(nameof(PaddingTopPx));
        OnPropertyChanged(nameof(PaddingRightPx));
        OnPropertyChanged(nameof(PaddingBottomPx));
    }

    partial void OnSelectedPresetChanged(UncropAspectPreset value)
    {
        if (_sourceImage is null || value.Ratio is not { } ratio)
        {
            return;
        }
        Padding = ComputeCenteredPadding(_sourceImage.FullBgr.Size(), ratio);
    }

    private void SetPaddingFromUser(CanvasPadding value)
    {
        if (Padding.Equals(value))
        {
            return;
        }
        Padding = value;
        if (SelectedPreset.Ratio is not null)
        {
            SelectedPreset = UncropAspectPresets.Custom;
        }
    }

    private static CanvasPadding ComputeCenteredPadding(Size sourceSize, double targetRatio)
    {
        double currentRatio = (double)sourceSize.Width / sourceSize.Height;
        if (targetRatio > currentRatio)
        {
            int targetWidth = (int)Math.Round(sourceSize.Height * targetRatio);
            int extra = Math.Max(0, targetWidth - sourceSize.Width);
            int half = extra / 2;
            return new CanvasPadding(half, 0, extra - half, 0);
        }
        else
        {
            int targetHeight = (int)Math.Round(sourceSize.Width / targetRatio);
            int extra = Math.Max(0, targetHeight - sourceSize.Height);
            int half = extra / 2;
            return new CanvasPadding(0, half, 0, extra - half);
        }
    }

    private void AdoptImage(LoadedImage image)
    {
        _sourceImage?.Dispose();
        _resultBgra?.Dispose();
        _resultBgra = null;
        _editHistory.Clear();
        IsDirty = false;
        RefreshUndoRedoState();
        SaveAsCommand.NotifyCanExecuteChanged();

        _sourceImage = image;
        SourceBitmap = image.FullBgr.ToBitmapSource();
        PreviewResult = null;
        IsImageLoaded = true;
        Padding = CanvasPadding.Zero;
        SelectedPreset = UncropAspectPresets.Free;
    }

    private bool CanApplyFill() => IsImageLoaded && !IsBusy
        && SelectedFillMode != UncropFillMode.AiOutpaint
        && !Padding.IsZero;

    private bool CanCancelFill() => IsBusy && _fillCts is not null && !_fillCts.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanCancelFill))]
    private void CancelFill()
    {
        if (_fillCts is not null && !_fillCts.IsCancellationRequested)
        {
            _fillCts.Cancel();
            StatusMessage = "Cancelling fill operation...";
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyFill))]
    private async Task ApplyFillAsync()
    {
        if (_sourceImage is null)
        {
            return;
        }

        var sourceBgr = _sourceImage.FullBgr;
        var padding = Padding;
        var mode = SelectedFillMode;
        var mirrorType = SelectedMirrorType;
        var mirrorBlur = MirrorBlurRadius;
        var mirrorFade = MirrorFadeOpacity;
        var inpaintMethod = SelectedInpaintMethod;
        var inpaintRadius = InpaintRadius;
        var blendMargin = BlendMargin;
        var inpaintPreFill = InpaintPreFillEdgeAverage;
        var blurred = BlurredColorFill;
        var blurRadius = BlurRadius;
        var replicateSmooth = ReplicateSmoothRadius;
        var zoomBlurRadius = ZoomBlurRadius;
        var zoomScale = ZoomScale;
        var gradientMode = SelectedGradientMode;
        var gradientNoise = GradientNoiseAmount;
        var patchSize = PatchSize;
        var patchOverlap = PatchBlendOverlap;
        var colorSource = SelectedColorSource;
        var customColor = colorSource == UncropColorSource.CustomColor
            ? new Scalar(CustomSolidColor.B, CustomSolidColor.G, CustomSolidColor.R)
            : (Scalar?)null;

        _fillCts?.Dispose();
        _fillCts = new CancellationTokenSource();
        var ct = _fillCts.Token;

        try
        {
            IsBusy = true;
            CancelFillCommand.NotifyCanExecuteChanged();
            StatusMessage = "Filling...";

            using var filledBgr = await Task.Run(() => mode switch
            {
                UncropFillMode.Mirror => _fillService.FillMirror(sourceBgr, padding, mirrorType, mirrorBlur, mirrorFade, ct),
                UncropFillMode.Inpaint => _fillService.FillInpaint(sourceBgr, padding, inpaintMethod, inpaintRadius, blendMargin, inpaintPreFill, ct),
                UncropFillMode.SolidColor => _fillService.FillSolidColor(sourceBgr, padding, blurred, customColor, blurRadius, ct),
                UncropFillMode.Replicate => _fillService.FillReplicate(sourceBgr, padding, replicateSmooth, ct),
                UncropFillMode.Wrap => _fillService.FillWrap(sourceBgr, padding, ct),
                UncropFillMode.ZoomBlur => _fillService.FillZoomBlur(sourceBgr, padding, zoomBlurRadius, zoomScale, blendMargin, ct),
                UncropFillMode.EdgeGradient => _fillService.FillEdgeGradient(sourceBgr, padding, gradientMode, customColor, gradientNoise, ct),
                UncropFillMode.PatchSynthesis => _fillService.FillPatchSynthesis(sourceBgr, padding, patchSize, patchOverlap, blendMargin, ct),
                _ => throw new InvalidOperationException($"Fill mode {mode} is not available yet.")
            }, ct);

            var bgra = new Mat();
            Cv2.CvtColor(filledBgr, bgra, ColorConversionCodes.BGR2BGRA);

            if (_resultBgra is not null)
            {
                _editHistory.Push(_resultBgra);
                _resultBgra.Dispose();
            }
            _resultBgra = bgra;

            RefreshUndoRedoState();
            SaveAsCommand.NotifyCanExecuteChanged();
            PreviewResult = _resultBgra.ToBitmapSource();
            IsDirty = true;
            StatusMessage = $"Applied {mode} fill.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Fill operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fill failed: {ex.Message}";
            _log.Error("Uncrop: fill failed", ex);
        }
        finally
        {
            _fillCts?.Dispose();
            _fillCts = null;
            IsBusy = false;
            CancelFillCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanUndoExecute() => _editHistory.CanUndo;
    private bool CanRedoExecute() => _editHistory.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    private void Undo()
    {
        if (_resultBgra is null)
        {
            return;
        }
        var restored = _editHistory.Undo(_resultBgra);
        if (restored is null)
        {
            return;
        }
        _resultBgra.Dispose();
        _resultBgra = restored;
        PreviewResult = _resultBgra.ToBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    private void Redo()
    {
        if (_resultBgra is null)
        {
            return;
        }
        var restored = _editHistory.Redo(_resultBgra);
        if (restored is null)
        {
            return;
        }
        _resultBgra.Dispose();
        _resultBgra = restored;
        PreviewResult = _resultBgra.ToBitmapSource();
        IsDirty = true;
        RefreshUndoRedoState();
    }

    private void RefreshUndoRedoState()
    {
        CanUndo = CanUndoExecute();
        CanRedo = CanRedoExecute();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => _resultBgra is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsAsync()
    {
        if (_resultBgra is null)
        {
            return;
        }

        var baseName = _sourceImage is not null ? Path.GetFileNameWithoutExtension(_sourceImage.FilePath) : "uncrop";
        var path = _dialogs.ShowSavePngDialog(baseName + "_uncrop.png", "Export Uncropped Image");
        if (path is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _imageExporter.ExportPngAsync(_resultBgra, path);
            IsDirty = false;
            StatusMessage = $"Exported to {path}";
            _log.Info($"Uncrop exported to {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _log.Error("Uncrop: export failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public override async Task ApplyAsync()
    {
        if (IsBusy)
        {
            return;
        }

        // If result is not generated yet, try generating if padding is set
        if (_resultBgra is null && CanApplyFill())
        {
            await ApplyFillAsync();
        }

        if (_resultBgra is not null)
        {
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(_resultBgra);
            _parentDocument.ApplyToolResult(bgr, alpha, "Uncrop Fill");
        }

        _shell.CloseTabDirect(this);
    }

    public override void Dispose()
    {
        _fillCts?.Cancel();
        _fillCts?.Dispose();
        _sourceImage?.Dispose();
        _resultBgra?.Dispose();
        _editHistory.Dispose();
    }
}
