using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
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
            ExportBlurRadius = ExportBlurRadius,
            ExportGradientTopColor = ExportGradientTopColor.ToString(),
            ExportGradientBottomColor = ExportGradientBottomColor.ToString(),
            ExportDropShadowEnabled = ExportDropShadowEnabled,
            ExportShadowOffset = ExportShadowOffset,
            ExportShadowBlur = ExportShadowBlur,
            ExportShadowOpacity = ExportShadowOpacity,
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

        ExportBlurRadius = p.ExportBlurRadius;
        if (!string.IsNullOrWhiteSpace(p.ExportGradientTopColor)
            && System.Windows.Media.ColorConverter.ConvertFromString(p.ExportGradientTopColor) is WpfColor top)
        {
            ExportGradientTopColor = top;
        }
        if (!string.IsNullOrWhiteSpace(p.ExportGradientBottomColor)
            && System.Windows.Media.ColorConverter.ConvertFromString(p.ExportGradientBottomColor) is WpfColor bottom)
        {
            ExportGradientBottomColor = bottom;
        }
        ExportDropShadowEnabled = p.ExportDropShadowEnabled;
        ExportShadowOffset = p.ExportShadowOffset;
        ExportShadowBlur = p.ExportShadowBlur;
        ExportShadowOpacity = p.ExportShadowOpacity;

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
            _editSession.Clear();
            RefreshUndoRedoState();
            _samEmbedding = null;
            _samPromptPointPreview = null;
            Sam.HasClickedPoint = false;
            _magicWandSeedPreview = null;
            MagicWand.HasClickedPoint = false;
            ProjectPath = null;
            GrabCut.SelectedRect = null;

            loaded = await _projectService.LoadAsync(path);
            preview = _downscaler.CreatePreview(loaded.OriginalBgr);

            var previewBitmap = loaded.OriginalAlpha is not null
                ? preview.Bgr.BuildPreviewWithAlpha(loaded.OriginalAlpha)
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

            ScribbleManager.Clear();
            GrabCut.HasScribbles = false;
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
}
