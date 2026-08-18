using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Tests.ViewModels;

public class ShellViewModelTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
        public void AddRecentFile(string path) => Current.RecentFiles.Insert(0, path);
        public void AddRecentProject(string path) => Current.RecentProjects.Insert(0, path);
    }

    private sealed class UnusedDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => throw new NotImplementedException();
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => throw new NotImplementedException();
        public string? ShowOpenFolderDialog(string title) => throw new NotImplementedException();
        public string? ShowOpenProjectDialog() => throw new NotImplementedException();
        public string? ShowSaveProjectDialog(string? suggestedFileName) => throw new NotImplementedException();
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => throw new NotImplementedException();
    }

    private static ShellViewModel CreateShell(FakeSettingsService settings) =>
        new(() => throw new NotImplementedException("Document factory not needed for this test"),
            () => throw new NotImplementedException("Uncrop factory not needed for this test"),
            new UnusedDialogService(),
            settings);

    [Fact]
    public void Constructor_PopulatesRecentListsFromSettings()
    {
        var settings = new FakeSettingsService();
        settings.Current.RecentFiles.Add("a.png");
        settings.Current.RecentFiles.Add("b.png");
        settings.Current.RecentProjects.Add("p.ibrproj");

        var shell = CreateShell(settings);

        Assert.Equal(new[] { "a.png", "b.png" }, shell.RecentFiles);
        Assert.Equal(new[] { "p.ibrproj" }, shell.RecentProjects);
    }

    [Fact]
    public void RefreshRecentFiles_ReplacesContentsWithCurrentSettingsState()
    {
        var settings = new FakeSettingsService();
        var shell = CreateShell(settings);
        Assert.Empty(shell.RecentFiles);

        settings.AddRecentFile("new.png");
        shell.RefreshRecentFiles();

        Assert.Equal(new[] { "new.png" }, shell.RecentFiles);
    }

    [Fact]
    public void RefreshRecentProjects_ReplacesContentsWithCurrentSettingsState()
    {
        var settings = new FakeSettingsService();
        var shell = CreateShell(settings);
        Assert.Empty(shell.RecentProjects);

        settings.AddRecentProject("new.ibrproj");
        shell.RefreshRecentProjects();

        Assert.Equal(new[] { "new.ibrproj" }, shell.RecentProjects);
    }

    [Fact]
    public void RefreshRecentFiles_DoesNotDuplicateEntries_WhenCalledRepeatedly()
    {
        var settings = new FakeSettingsService();
        settings.Current.RecentFiles.Add("only.png");
        var shell = CreateShell(settings);

        shell.RefreshRecentFiles();
        shell.RefreshRecentFiles();

        Assert.Equal(new[] { "only.png" }, shell.RecentFiles);
    }

    [Fact]
    public async Task NewProjectAsync_WhenImageSelected_OpensDocumentInNewTab()
    {
        var settings = new FakeSettingsService();
        var fakeDialogs = new FakeImageDialogService("photo.png");

        var docVm = new DocumentViewModel(
            new FakeImageLoaderService(),
            new FakeImageExportService(),
            new FakeDownscaleService(),
            fakeDialogs,
            new FakeBatchProcessingService(),
            settings,
            new FakeProjectService(),
            new FakeFileLogService(),
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            new BackgroundImageRemover.Services.Strategies.OnnxStrategy(new BackgroundImageRemover.Services.Onnx.OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new BackgroundImageRemover.Services.Strategies.GrabCutStrategy(),
            new BackgroundImageRemover.Services.Strategies.SamStrategy(new BackgroundImageRemover.Services.Sam.SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());

        var shell = new ShellViewModel(
            () => docVm,
            () => throw new InvalidOperationException("Should not create uncrop tab directly"),
            fakeDialogs,
            settings);

        await shell.NewProjectCommand.ExecuteAsync(null);

        Assert.Single(shell.Documents);
        Assert.Same(docVm, shell.SelectedDocument);
    }

    private sealed class FakeImageDialogService : IDialogService
    {
        private readonly string? _chosenPath;
        public FakeImageDialogService(string? chosenPath) => _chosenPath = chosenPath;

        public string? ShowOpenImageDialog() => _chosenPath;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => null;
        public string? ShowOpenFolderDialog(string title) => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
    }

    private sealed class FakeUncropFillService : BackgroundImageRemover.Services.Outpaint.IUncropFillService
    {
        public OpenCvSharp.Mat ExpandCanvas(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, out OpenCvSharp.Mat newAreaMask)
        {
            newAreaMask = new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC1);
            return new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3);
        }
        public OpenCvSharp.Mat FillInpaint(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, Models.UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillMirror(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, Models.UncropMirrorType mirrorType = Models.UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillSolidColor(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, bool blurred, OpenCvSharp.Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillReplicate(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillWrap(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillZoomBlur(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillEdgeGradient(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, Models.UncropGradientMode gradientMode = Models.UncropGradientMode.PerEdgeSplay, OpenCvSharp.Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillPatchSynthesis(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
    }

    private sealed class FakeImageLoaderService : BackgroundImageRemover.Services.ImageIo.IImageLoaderService
    {
        public Task<Models.LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new Models.LoadedImage(path, new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)));
    }

    private sealed class FakeImageExportService : BackgroundImageRemover.Services.ImageIo.IImageExportService
    {
        public Task ExportPngAsync(OpenCvSharp.Mat imageBgra, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFileLogService : BackgroundImageRemover.Services.Logging.IFileLogService
    {
        public void Debug(string message) { }
        public void Error(string message, Exception? ex = null) { }
        public void Info(string message) { }
        public void Warn(string message) { }
    }

    private sealed class FakeDownscaleService : BackgroundImageRemover.Services.Preview.IDownscaleService
    {
        public Models.PreviewImage CreatePreview(OpenCvSharp.Mat full, int maxDim = 800)
            => new(full.Clone(), 1.0);
    }

    private sealed class FakeBatchProcessingService : BackgroundImageRemover.Services.Batch.IBatchProcessingService
    {
        public Task RunAsync(IReadOnlyList<string> filePaths, BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy strategy, BackgroundImageRemover.Services.Strategies.StrategyContext context, string outputFolder, IProgress<BackgroundImageRemover.Services.Batch.BatchProgress>? progress = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProjectService : BackgroundImageRemover.Services.Projects.IProjectService
    {
        public Task SaveAsync(string path, OpenCvSharp.Mat originalBgr, OpenCvSharp.Mat? originalAlpha, OpenCvSharp.Mat? workingBgr, OpenCvSharp.Mat? workingAlpha, BackgroundImageRemover.Models.ProjectDocument settings, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<BackgroundImageRemover.Services.Projects.LoadedProject> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new BackgroundImageRemover.Services.Projects.LoadedProject
            {
                Settings = new BackgroundImageRemover.Models.ProjectDocument(),
                OriginalBgr = new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)
            });
    }

    private sealed class FakeModelCacheService : BackgroundImageRemover.Services.Onnx.IModelCacheService
    {
        public string CachedModelPath(BackgroundImageRemover.Models.OnnxModelKind kind) => "";
        public bool IsModelCached(BackgroundImageRemover.Models.OnnxModelKind kind) => true;
        public Task<string> EnsureModelAvailableAsync(BackgroundImageRemover.Models.OnnxModelKind kind, IProgress<BackgroundImageRemover.Services.Onnx.ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<BackgroundImageRemover.Services.Onnx.ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public bool IsNamedFileCached(string fileName) => true;
    }
}
