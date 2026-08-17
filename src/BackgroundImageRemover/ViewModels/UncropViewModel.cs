using System.IO;
using System.Windows.Media.Imaging;
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

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// State and logic for the standalone Uncrop (canvas outpainting) window. Independent of
/// <see cref="DocumentViewModel"/>/the background-removal strategy pipeline: it can be opened
/// empty (with its own "Open image...") or seeded with a document's current image.
/// </summary>
public partial class UncropViewModel : ObservableObject, IDisposable
{
    private readonly IUncropFillService _fillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly EditHistory _editHistory = new();

    private LoadedImage? _sourceImage;
    private Mat? _resultBgra;

    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;
    public IReadOnlyList<UncropInpaintMethod> InpaintMethods { get; } = Enum.GetValues<UncropInpaintMethod>();

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
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
    private UncropInpaintMethod _selectedInpaintMethod = UncropInpaintMethod.Telea;

    [ObservableProperty]
    private bool _blurredColorFill;

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
        Padding = ComputeCenteredPadding(_sourceImage.FullBgr.Size(), ratio);
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

    /// <summary>Seeds the window with an image handed in from the main window (a clone, so this
    /// window's own lifecycle/EditHistory never touches the source document's Mats).</summary>
    public void LoadInitialImage(LoadedImage image) => AdoptImage(image);

    private bool CanOpenImage() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenImage))]
    private async Task OpenImageAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading image...";
            var image = await _imageLoader.LoadAsync(path);
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

    private void AdoptImage(LoadedImage image)
    {
        _sourceImage?.Dispose();
        _resultBgra?.Dispose();
        _resultBgra = null;
        _editHistory.Clear();
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
        var inpaintMethod = SelectedInpaintMethod;
        var blurred = BlurredColorFill;

        try
        {
            IsBusy = true;
            StatusMessage = "Filling...";

            using var filledBgr = await Task.Run(() => mode switch
            {
                UncropFillMode.Mirror => _fillService.FillMirror(sourceBgr, padding),
                UncropFillMode.Inpaint => _fillService.FillInpaint(sourceBgr, padding, inpaintMethod),
                UncropFillMode.SolidColor => _fillService.FillSolidColor(sourceBgr, padding, blurred),
                _ => throw new InvalidOperationException($"Fill mode {mode} is not available yet.")
            });

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
            StatusMessage = $"Applied {mode} fill.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fill failed: {ex.Message}";
            _log.Error("Uncrop: fill failed", ex);
        }
        finally
        {
            IsBusy = false;
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
        _sourceImage?.Dispose();
        _resultBgra?.Dispose();
        _editHistory.Dispose();
    }
}
