using System.IO;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
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
/// State and logic for the standalone Uncrop (canvas outpainting) window. Independent of
/// <see cref="DocumentViewModel"/>/the background-removal strategy pipeline: it can be opened
/// empty (with its own "Open image...") or seeded with a document's current image.
/// </summary>
public partial class UncropViewModel : ObservableObject, IDocumentTab
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

    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;
    public IReadOnlyList<UncropInpaintMethod> InpaintMethods { get; } = Enum.GetValues<UncropInpaintMethod>();
    public IReadOnlyList<UncropMirrorType> MirrorTypes { get; } = Enum.GetValues<UncropMirrorType>();
    public IReadOnlyList<UncropColorSource> ColorSources { get; } = Enum.GetValues<UncropColorSource>();
    public IReadOnlyList<UncropGradientMode> GradientModes { get; } = Enum.GetValues<UncropGradientMode>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _title = "Uncrop";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirtyHint))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private bool _isDirty;

    public string? DirtyHint => IsDirty ? "Unsaved changes — export or save your result." : null;
    public bool IsCutout => false;
    public string? CutoutHint => null;
    public string WindowTitle => Title + (IsDirty ? " *" : string.Empty) + " — Background Image Remover";
    public string TabTitle => IsDirty ? Title + " *" : Title;

    public Task<bool> TrySaveProjectAsync()
    {
        // For uncrop, save prompt can trigger SaveAs
        if (_resultBgra is null)
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(true);
    }

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenImageCommand))]
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

    public UncropViewModel(
        IUncropFillService fillService,
        IDialogService dialogs,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IFileLogService log)
    {
        _fillService = fillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;
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
        Padding = CanvasPadding.ComputeCentered(_sourceImage.FullBgr.Size(), ratio);
    }

    /// <summary>Applies a padding change coming from the handles or the numeric fields: if a
    /// specific ratio preset was active, hand-editing drops it to "Custom" so the preset buttons
    /// stop fighting the manual change.</summary>
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


    /// <summary>Seeds the window with an image handed in from the main window (a clone, so this
    /// window's own lifecycle/EditHistory never touches the source document's Mats).</summary>
    public void LoadInitialImage(LoadedImage image) => AdoptImage(image);

    public async Task LoadAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading image...";
            var image = await _imageLoader.LoadAsync(path);
            Title = Path.GetFileName(path) + " (Uncrop)";
            AdoptImage(image);
            StatusMessage = $"Loaded {Path.GetFileName(path)} ({image.FullBgr.Width}x{image.FullBgr.Height})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            _log.Error("Uncrop: could not load image", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenImage() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenImage))]
    private async Task OpenImageAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        await LoadAsync(path);
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
        if (string.IsNullOrEmpty(Title) || Title == "Uncrop")
        {
            Title = !string.IsNullOrEmpty(image.FilePath) ? Path.GetFileName(image.FilePath) + " (Uncrop)" : "Uncrop";
        }
        SourceBitmap = image.FullBgr.ToBitmapSource();
        PreviewResult = null;
        IsImageLoaded = true;
        Padding = CanvasPadding.Zero;
        SelectedPreset = UncropAspectPresets.Free;
    }

    private bool CanApplyFill() => IsImageLoaded && !IsBusy
        && UncropOperationHelper.CanExecute(new UncropOperationHelper.UncropConfig
        {
            FillMode = SelectedFillMode,
            Padding = Padding
        });

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

        var config = new UncropOperationHelper.UncropConfig
        {
            Padding = Padding,
            FillMode = SelectedFillMode,
            MirrorType = SelectedMirrorType,
            MirrorBlurRadius = MirrorBlurRadius,
            MirrorFadeOpacity = MirrorFadeOpacity,
            InpaintMethod = SelectedInpaintMethod,
            InpaintRadius = InpaintRadius,
            BlendMargin = BlendMargin,
            InpaintPreFillEdgeAverage = InpaintPreFillEdgeAverage,
            BlurredColorFill = BlurredColorFill,
            BlurRadius = BlurRadius,
            ReplicateSmoothRadius = ReplicateSmoothRadius,
            ZoomBlurRadius = ZoomBlurRadius,
            ZoomScale = ZoomScale,
            GradientMode = SelectedGradientMode,
            GradientNoiseAmount = GradientNoiseAmount,
            PatchSize = PatchSize,
            PatchBlendOverlap = PatchBlendOverlap,
            ColorSource = SelectedColorSource,
            CustomSolidColor = CustomSolidColor
        };

        _fillCts?.Dispose();
        _fillCts = new CancellationTokenSource();
        var ct = _fillCts.Token;

        try
        {
            IsBusy = true;
            CancelFillCommand.NotifyCanExecuteChanged();
            StatusMessage = "Filling...";

            using var filledBgr = await UncropOperationHelper.ExecuteUncropAsync(
                _sourceImage.FullBgr, config, _fillService, ct);

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
            StatusMessage = $"Applied {config.FillMode} fill.";
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

    public void Dispose()
    {
        _fillCts?.Cancel();
        _fillCts?.Dispose();
        _sourceImage?.Dispose();
        _resultBgra?.Dispose();
        _editHistory.Dispose();
    }
}
