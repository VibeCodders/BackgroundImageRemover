using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Autosave;
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

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the autosave/crash-recovery contract: dirty documents are snapshotted into the
/// recovery folder (with a manifest), snapshots are dropped as soon as a document is saved or
/// closed, and leftover snapshots can be restored as dirty tabs with their original names.
/// </summary>
public class AutosaveServiceTests
{
    private static AutosaveService CreateService(
        ShellViewModel shell,
        ISettingsService settings,
        string recoveryDir,
        IProjectService projectService)
    {
        var service = new AutosaveService(
            settings, projectService, new FakeFileLogService(), recoveryDir);
        service.Start(shell);
        return service;
    }

    [Fact]
    public async Task RunAutosaveAsync_DirtyDocument_WritesSnapshotAndManifestEntry()
    {
        using var harness = new Harness();
        var doc = await harness.NewDirtyDocumentAsync();

        await harness.Service.RunAutosaveAsync();

        var pending = harness.Service.PendingRecovery;
        Assert.Single(pending);
        Assert.Equal(doc.Title, pending[0].Title);
        Assert.True(File.Exists(pending[0].FilePath));
        Assert.True(File.Exists(Path.Combine(harness.RecoveryDir, "manifest.json")));
    }

    [Fact]
    public async Task RunAutosaveAsync_CleanDocument_IsNotSnapshotted()
    {
        using var harness = new Harness();
        var doc = await harness.NewDocumentAsync();
        Assert.False(doc.IsDirty);

        await harness.Service.RunAutosaveAsync();

        Assert.Empty(harness.Service.PendingRecovery);
        Assert.False(File.Exists(Path.Combine(harness.RecoveryDir, "manifest.json")));
    }

    [Fact]
    public async Task SavingDocument_RemovesItsRecoverySnapshot()
    {
        using var harness = new Harness();
        var doc = await harness.NewDirtyDocumentAsync();
        await harness.Service.RunAutosaveAsync();
        Assert.Single(harness.Service.PendingRecovery);

        // Ctrl+S clears the dirty flag; the snapshot is now obsolete.
        doc.IsDirty = false;
        await harness.Service.RunAutosaveAsync();

        Assert.Empty(harness.Service.PendingRecovery);
        Assert.Empty(Directory.GetFiles(harness.RecoveryDir, "*.ibrproj"));
    }

    [Fact]
    public async Task ClosingDocument_RemovesItsRecoverySnapshot()
    {
        using var harness = new Harness();
        var doc = await harness.NewDirtyDocumentAsync();
        await harness.Service.RunAutosaveAsync();
        Assert.Single(harness.Service.PendingRecovery);

        harness.Shell.Documents.Remove(doc);
        doc.Dispose();

        Assert.Empty(harness.Service.PendingRecovery);
        Assert.Empty(Directory.GetFiles(harness.RecoveryDir, "*.ibrproj"));
    }

    [Fact]
    public async Task RemoveRecoveryEntry_AfterRestore_DeletesSnapshotAndKeepsNothingPending()
    {
        using var harness = new Harness();
        var doc = await harness.NewDirtyDocumentAsync();
        await harness.Service.RunAutosaveAsync();
        var entry = Assert.Single(harness.Service.PendingRecovery);

        harness.Service.RemoveRecoveryEntry(entry.Id);

        Assert.False(File.Exists(entry.FilePath));
        Assert.Empty(harness.Service.PendingRecovery);
        Assert.False(harness.Service.HasPendingRecovery);
    }

    [Fact]
    public async Task RestoreFlow_OpensSnapshotAsDirtyTabWithOriginalTitle()
    {
        using var harness = new Harness();
        var doc = await harness.NewDirtyDocumentAsync();
        await harness.Service.RunAutosaveAsync();
        var entry = Assert.Single(harness.Service.PendingRecovery);

        // What App.OnStartup does after the user confirms the restore prompt.
        await harness.Shell.OpenInNewTabAsync(entry.FilePath, entry.Title);
        harness.Service.RemoveRecoveryEntry(entry.Id);

        var restored = Assert.IsType<DocumentViewModel>(harness.Shell.Documents[1]);
        Assert.Equal(doc.Title, restored.Title);
        Assert.True(restored.IsDirty);
        Assert.Null(restored.ProjectPath);
        Assert.True(restored.IsImageLoaded);
        Assert.Empty(harness.Service.PendingRecovery);
        restored.Dispose();
    }

    [Fact]
    public async Task DiscardAllRecovery_DeletesEverything()
    {
        using var harness = new Harness();
        await harness.NewDirtyDocumentAsync();
        await harness.Service.RunAutosaveAsync();
        Assert.True(harness.Service.HasPendingRecovery);

        harness.Service.DiscardAllRecovery();

        Assert.False(harness.Service.HasPendingRecovery);
        Assert.False(Directory.Exists(harness.RecoveryDir));
    }

    /// <summary>Builds a shell, a real project service and an autosave service over a temp folder.</summary>
    private sealed class Harness : IDisposable
    {
        public string RecoveryDir { get; } = Path.Combine(Path.GetTempPath(), $"autosave_test_{Guid.NewGuid():N}");

        public FakeSettingsService Settings { get; } = new();

        public FakeProjectService ProjectService { get; } = new();

        public ShellViewModel Shell { get; }

        public AutosaveService Service { get; }

        public Harness()
        {
            Settings.Current.EnableAutosave = false; // no timer in tests; RunAutosaveAsync is called directly
            Shell = CreateShell(Settings, ProjectService);
            Service = CreateService(Shell, Settings, RecoveryDir, ProjectService);
        }

        public async Task<DocumentViewModel> NewDocumentAsync()
        {
            var doc = Shell.Documents.Count == 0
                ? CreateDocument(Settings, ProjectService)
                : throw new InvalidOperationException("Harness supports a single open document at a time.");
            Shell.Documents.Add(doc);
            await doc.LoadImageAsync("photo.png");
            return doc;
        }

        public async Task<DocumentViewModel> NewDirtyDocumentAsync()
        {
            var doc = await NewDocumentAsync();
            using var bgr = new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30));
            using var alpha = new Mat(4, 4, MatType.CV_8UC1, new Scalar(255));
            doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Test edit");
            Assert.True(doc.IsDirty);
            return doc;
        }

        public void Dispose()
        {
            foreach (var tab in Shell.Documents.OfType<DocumentViewModel>())
            {
                tab.Dispose();
            }
            Service.Dispose();
            try
            {
                if (Directory.Exists(RecoveryDir))
                {
                    Directory.Delete(RecoveryDir, true);
                }
            }
            catch
            {
                // best effort
            }
        }
    }

    private static ShellViewModel CreateShell(ISettingsService settings, IProjectService projectService)
    {
        var dialogs = new FakeDialogService();
        return new ShellViewModel(
            () => CreateDocument(settings, projectService),
            () => throw new InvalidOperationException("Uncrop factory not needed in autosave tests"),
            dialogs,
            settings,
            new FakeDownscaleService(),
            new FakeFileLogService(),
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService(),
            new FakeImageLoaderService(),
            new FakeImageExportService());
    }

    private static DocumentViewModel CreateDocument(ISettingsService settings, IProjectService projectService) =>
        new(
            new FakeImageLoaderService(),
            new FakeImageExportService(),
            new FakeDownscaleService(),
            new FakeDialogService(),
            new FakeBatchProcessingService(),
            settings,
            projectService,
            new FakeFileLogService(),
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
        public void AddRecentFile(string path) { }
        public void AddRecentProject(string path) { }
        public void ClearRecentFiles() { }
        public void ClearRecentProjects() { }
    }

    private sealed class FakeProjectService : IProjectService
    {
        private readonly ProjectService _inner = new();

        public Task SaveAsync(string path, Mat originalBgr, Mat? originalAlpha, Mat? workingBgr, Mat? workingAlpha, ProjectDocument settings, CancellationToken ct = default)
            => _inner.SaveAsync(path, originalBgr, originalAlpha, workingBgr, workingAlpha, settings, ct);

        public Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default)
            => _inner.LoadAsync(path, ct);
    }

    private sealed class FakeImageLoaderService : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30))));
    }

    private sealed class FakeImageExportService : IImageExportService
    {
        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ExportWebpAsync(Mat bgra, string destinationPath, int quality = 90, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => null;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => null;
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG") => null;
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP") => null;
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private sealed class FakeBatchProcessingService : IBatchProcessingService
    {
        public Task RunAsync(IReadOnlyList<string> inputFiles, IBackgroundRemovalStrategy strategy, StrategyContext context, string outputFolder, IProgress<BatchProgress>? progress, CancellationToken ct, BatchExportOptions? exportOptions = null)
            => Task.CompletedTask;
    }

    private sealed class FakeFileLogService : IFileLogService
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class FakeDownscaleService : IDownscaleService
    {
        public PreviewImage CreatePreview(Mat full, int maxDim = 800) => new(full.Clone(), 1.0);
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
