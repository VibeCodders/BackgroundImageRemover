using System.IO;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
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

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var clipboardBitmap = ViewInteractionHelper.TryGetClipboardImage();
        if (clipboardBitmap is null)
        {
            StatusMessage = "No image found in clipboard.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Pasting image from clipboard...";
            var loaded = await _imageLoader.LoadFromBitmapSourceAsync(clipboardBitmap, "Clipboard Image");
            await InitializeLoadedImageAsync(loaded, "Clipboard Image");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not paste image: {ex.Message}";
            _log.Error("Could not paste image from clipboard", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading image...";
            var loaded = await _imageLoader.LoadAsync(path);
            await InitializeLoadedImageAsync(loaded, path);
            _settings.AddRecentFile(path);
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

    private async Task InitializeLoadedImageAsync(LoadedImage loaded, string sourceName)
    {
        _previewCts?.Cancel();

        _loadedImage?.Dispose();
        _preview?.Dispose();
        DisposeWorkingResult();
        _editHistory.Clear();
        RefreshUndoRedoState();
        _samEmbedding = null;
        _samPromptPointPreview = null;
        Sam.HasClickedPoint = false;

        _loadedImage = loaded;
        var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
        _preview = preview;

        bool isActualCutout = BackgroundCompositingService.HasMeaningfulTransparency(_loadedImage.FullAlpha);
        PreviewBitmap = isActualCutout
            ? BuildPreviewBitmapWithAlpha(preview, _loadedImage.FullAlpha!)
            : preview.Bgr.ToBitmapSource();
        ResultBitmap = null;
        IsImageLoaded = true;
        IsCutout = isActualCutout;
        var displayTitle = Path.GetFileName(sourceName);
        if (string.IsNullOrWhiteSpace(displayTitle)) displayTitle = sourceName;
        Title = IsCutout ? displayTitle + " (cutout)" : displayTitle;
        StatusMessage = $"Loaded {displayTitle} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
        _log.Info($"Loaded image {sourceName} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})");

        GrabCut.SelectedRect = null;
        ClearScribbles();
        ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);

        if (SelectedStrategy == StrategyKind.Sam && Sam.IsModelReady)
        {
            ComputeSamEmbedding();
        }

        if (isActualCutout)
        {
            AdoptLoadedCutout();
        }
        else
        {
            RequestPreviewDebounced();
        }
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
}
