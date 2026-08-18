using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the contract of the document-level Adjustments panel (Apply Adjustments button):
/// the operation must be undoable through the normal history and must never drop the source
/// image's alpha channel (a regression where the loaded image was rebuilt without it).
/// </summary>
public class DocumentViewModelAdjustmentsTests
{
    [Fact]
    public async Task ApplyAdjustments_RecordsUndoStepAndPreservesSourceAlpha()
    {
        var doc = CreateDocument(new AlphaImageLoader());

        await doc.LoadImageAsync("cutout.png");

        Assert.NotNull(doc.LoadedImageForUncrop!.FullAlpha);
        Assert.False(doc.UndoCommand.CanExecute(null));

        doc.AdjBrightness = 50;
        await doc.ApplyAdjustmentsCommand.ExecuteAsync(null);

        Assert.True(doc.HasWorkingResult);
        Assert.True(doc.UndoCommand.CanExecute(null), "adjustments must be undoable");
        Assert.NotNull(doc.LoadedImageForUncrop!.FullAlpha);
        Assert.Contains(doc.EditSteps, s => s.Name == "Adjustments" && !s.IsUndone);

        doc.UndoCommand.Execute(null);

        Assert.False(doc.UndoCommand.CanExecute(null));
        Assert.Contains(doc.EditSteps, s => s.Name == "Adjustments" && s.IsUndone);

        doc.RedoCommand.Execute(null);

        Assert.True(doc.UndoCommand.CanExecute(null));
        Assert.Contains(doc.EditSteps, s => s.Name == "Adjustments" && !s.IsUndone);
    }

    [Fact]
    public async Task ApplyAdjustments_OnPlainPhoto_CreatesUndoableWorkingResult()
    {
        var doc = CreateDocument(new PlainImageLoader());
        await doc.LoadImageAsync("photo.jpg");
        Assert.False(doc.HasWorkingResult);

        doc.AdjContrast = 1.5;
        await doc.ApplyAdjustmentsCommand.ExecuteAsync(null);

        Assert.True(doc.HasWorkingResult);
        Assert.True(doc.UndoCommand.CanExecute(null));
        Assert.Contains(doc.EditSteps, s => s.Name == "Adjustments" && !s.IsUndone);
    }

    [Fact]
    public async Task ApplyAdjustments_WithIdentityValues_DoesNothing()
    {
        var doc = CreateDocument(new PlainImageLoader());
        await doc.LoadImageAsync("photo.jpg");

        await doc.ApplyAdjustmentsCommand.ExecuteAsync(null);

        Assert.False(doc.HasWorkingResult);
        Assert.False(doc.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportJpg_UsesConfiguredJpegQuality()
    {
        var exporter = new RecordingImageExportService();
        var doc = CreateDocument(new AlphaImageLoader(), exporter, new FakeDialogServiceWithJpgPath("out.jpg"));
        await doc.LoadImageAsync("cutout.png");
        Assert.True(doc.HasWorkingResult); // the loaded cutout is adopted as the working result

        doc.ExportBackgroundMode = ExportBackgroundMode.Transparent;
        doc.ExportJpegQuality = 60;
        await doc.ExportJpgCommand.ExecuteAsync(null);

        Assert.NotNull(exporter.LastJpgPath);
        Assert.Equal(60, exporter.LastJpgQuality);
    }

    [Fact]
    public async Task ExportJpg_DefaultsToHighQuality()
    {
        var exporter = new RecordingImageExportService();
        var doc = CreateDocument(new AlphaImageLoader(), exporter, new FakeDialogServiceWithJpgPath("out.jpg"));
        await doc.LoadImageAsync("cutout.png");

        await doc.ExportJpgCommand.ExecuteAsync(null);

        Assert.NotNull(exporter.LastJpgPath);
        Assert.Equal(95, exporter.LastJpgQuality);
    }

    [Fact]
    public async Task Batch_RemembersInputAndOutputFoldersInSettings()
    {
        var inputDir = Path.Combine(Path.GetTempPath(), $"batch_in_{Guid.NewGuid():N}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"batch_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(inputDir);
        try
        {
            // A real 1x1 PNG so the folder is recognized as containing supported images.
            using var img = new Mat(1, 1, MatType.CV_8UC3, new Scalar(1, 2, 3));
            Cv2.ImWrite(Path.Combine(inputDir, "a.png"), img);

            var settings = new FakeSettingsService();
            var dialogs = new BatchFolderDialogService(inputDir, outputDir);
            var doc = CreateDocument(
                new AlphaImageLoader(),
                settings: settings,
                dialogs: dialogs,
                strategies: new IBackgroundRemovalStrategy[] { new ChromaKeyStrategy() });
            await doc.LoadImageAsync("cutout.png");

            await doc.BatchCommand.ExecuteAsync(null);

            Assert.Equal(inputDir, settings.Current.LastBatchInputFolder);
            Assert.Equal(outputDir, settings.Current.LastBatchOutputFolder);
        }
        finally
        {
            Directory.Delete(inputDir, true);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
        }
    }

    private static DocumentViewModel CreateDocument(
        IImageLoaderService loader,
        IImageExportService? exporter = null,
        IDialogService? dialogs = null,
        FakeSettingsService? settings = null,
        IEnumerable<IBackgroundRemovalStrategy>? strategies = null)
    {
        settings ??= new FakeSettingsService();
        return new DocumentViewModel(
            loader,
            exporter ?? new FakeImageExportService(),
            new FakeDownscaleService(),
            dialogs ?? new FakeDialogService(),
            new FakeBatchProcessingService(),
            settings,
            new FakeProjectService(),
            new FakeFileLogService(),
            strategies ?? Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());
    }

    /// <summary>Returns the input folder for the first folder prompt and the output folder for the second.</summary>
    private sealed class BatchFolderDialogService : FakeDialogService
    {
        private readonly string _inputDir;
        private readonly string _outputDir;
        private int _folderCalls;

        public BatchFolderDialogService(string inputDir, string outputDir)
        {
            _inputDir = inputDir;
            _outputDir = outputDir;
        }

        public override string? ShowOpenFolderDialog(string title, string? initialDirectory = null)
        {
            _folderCalls++;
            return _folderCalls == 1 ? _inputDir : _outputDir;
        }

        public override BatchExportOptions? ShowBatchOptionsDialog() => new() { ExportJpeg = false };
    }

    private sealed class RecordingImageExportService : IImageExportService
    {
        public string? LastJpgPath { get; private set; }
        public int LastJpgQuality { get; private set; }

        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default)
        {
            LastJpgPath = destinationPath;
            LastJpgQuality = quality;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogServiceWithJpgPath : FakeDialogService
    {
        private readonly string? _jpgPath;
        public FakeDialogServiceWithJpgPath(string? jpgPath) => _jpgPath = jpgPath;

        public override string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG") => _jpgPath;
    }

    private sealed class AlphaImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
        {
            var bgr = new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30));
            var alpha = new Mat(4, 4, MatType.CV_8UC1, new Scalar(0));
            return Task.FromResult(new LoadedImage(path, bgr, alpha));
        }
    }

    private sealed class PlainImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30))));
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
        public void AddRecentFile(string path) { }
        public void AddRecentProject(string path) { }
        public void ClearRecentFiles() { }
        public void ClearRecentProjects() { }
    }

    private class FakeDialogService : IDialogService
    {
        public virtual string? ShowOpenImageDialog() => null;
        public virtual string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => null;
        public virtual string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG") => null;
        public virtual string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
        public virtual string? ShowOpenProjectDialog() => null;
        public virtual string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public virtual BatchExportOptions? ShowBatchOptionsDialog() => null;
        public virtual CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
    }

    private sealed class FakeImageExportService : IImageExportService
    {
        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDownscaleService : IDownscaleService
    {
        public PreviewImage CreatePreview(Mat full, int maxDim = 800) => new(full.Clone(), 1.0);
    }

    private sealed class FakeBatchProcessingService : IBatchProcessingService
    {
        public Task RunAsync(
            IReadOnlyList<string> inputFiles,
            IBackgroundRemovalStrategy strategy,
            StrategyContext context,
            string outputFolder,
            IProgress<BatchProgress>? progress,
            CancellationToken ct,
            BatchExportOptions? exportOptions = null)
            => Task.CompletedTask;
    }

    private sealed class FakeProjectService : IProjectService
    {
        public Task SaveAsync(string path, Mat originalBgr, Mat? originalAlpha, Mat? workingBgr, Mat? workingAlpha, ProjectDocument settings, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedProject
            {
                Settings = new ProjectDocument(),
                OriginalBgr = new Mat(1, 1, MatType.CV_8UC3)
            });
    }

    private sealed class FakeFileLogService : IFileLogService
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class FakeModelCacheService : IModelCacheService
    {
        public string CachedModelPath(OnnxModelKind kind) => "";
        public bool IsModelCached(OnnxModelKind kind) => true;
        public Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public bool IsNamedFileCached(string fileName) => true;
    }

    private sealed class FakeUncropFillService : IUncropFillService
    {
        public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
        {
            newAreaMask = new Mat(1, 1, MatType.CV_8UC1);
            return new Mat(1, 1, MatType.CV_8UC3);
        }
        public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillZoomBlur(Mat sourceBgr, CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillEdgeGradient(Mat sourceBgr, CanvasPadding padding, UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay, Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillPatchSynthesis(Mat sourceBgr, CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
    }
}
