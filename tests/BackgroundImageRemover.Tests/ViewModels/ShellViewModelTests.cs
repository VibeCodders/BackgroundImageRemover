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
        public void ClearRecentFiles() => Current.RecentFiles.Clear();
        public void ClearRecentProjects() => Current.RecentProjects.Clear();
    }

    private sealed class UnusedDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => throw new NotImplementedException();
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => throw new NotImplementedException();
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG") => throw new NotImplementedException();
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowOpenProjectDialog() => throw new NotImplementedException();
        public string? ShowSaveProjectDialog(string? suggestedFileName) => throw new NotImplementedException();
        public BackgroundImageRemover.Models.BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => throw new NotImplementedException();
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private static ShellViewModel CreateShell(FakeSettingsService settings, IDialogService? dialogs = null)
    {
        var log = new FakeFileLogService();
        var modelCache = new FakeModelCacheService();
        var onnxEngine = new BackgroundImageRemover.Services.Onnx.OnnxInferenceEngine(modelCache, log);
        var samEngine = new BackgroundImageRemover.Services.Sam.SamInferenceEngine(modelCache);
        var onnxStrategy = new BackgroundImageRemover.Services.Strategies.OnnxStrategy(onnxEngine);
        var grabCutStrategy = new BackgroundImageRemover.Services.Strategies.GrabCutStrategy();
        var samStrategy = new BackgroundImageRemover.Services.Strategies.SamStrategy(samEngine);
        var uncropFillService = new FakeUncropFillService();
        var imageLoader = new FakeImageLoaderService();
        var imageExporter = new FakeImageExportService();
        var downscaler = new FakeDownscaleService();

        return new ShellViewModel(
            () => throw new NotImplementedException("Document factory not needed for this test"),
            () => throw new NotImplementedException("Uncrop factory not needed for this test"),
            dialogs ?? new UnusedDialogService(),
            settings,
            downscaler,
            log,
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            onnxStrategy,
            grabCutStrategy,
            samStrategy,
            uncropFillService,
            imageLoader,
            imageExporter);
    }

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
    public void ClearRecentFiles_EmptiesBothListAndSettings()
    {
        var settings = new FakeSettingsService();
        settings.AddRecentFile("a.png");
        settings.AddRecentFile("b.png");
        settings.AddRecentProject("p.ibrproj");
        var shell = CreateShell(settings);
        shell.RefreshRecentFiles();
        shell.RefreshRecentProjects();
        Assert.NotEmpty(shell.RecentFiles);
        Assert.NotEmpty(shell.RecentProjects);

        shell.ClearRecentFilesCommand.Execute(null);
        shell.ClearRecentProjectsCommand.Execute(null);

        Assert.Empty(shell.RecentFiles);
        Assert.Empty(shell.RecentProjects);
        Assert.Empty(settings.Current.RecentFiles);
        Assert.Empty(settings.Current.RecentProjects);
    }

    [Fact]
    public async Task DuplicateTab_OpensCopyOfCurrentDocument()
    {
        var settings = new FakeSettingsService();
        var fakeDialogs = new FakeImageDialogService("photo.png");

        Func<DocumentViewModel> docFactory = () => new DocumentViewModel(
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
            docFactory,
            () => throw new InvalidOperationException("Should not create uncrop tab directly"),
            fakeDialogs,
            settings,
            new FakeDownscaleService(),
            new FakeFileLogService(),
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            new BackgroundImageRemover.Services.Strategies.OnnxStrategy(new BackgroundImageRemover.Services.Onnx.OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new BackgroundImageRemover.Services.Strategies.GrabCutStrategy(),
            new BackgroundImageRemover.Services.Strategies.SamStrategy(new BackgroundImageRemover.Services.Sam.SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService(),
            new FakeImageLoaderService(),
            new FakeImageExportService());

        await shell.NewProjectCommand.ExecuteAsync(null);
        Assert.Single(shell.Documents);
        var docVm = Assert.IsType<DocumentViewModel>(shell.Documents[0]);
        Assert.True(docVm.IsImageLoaded);

        shell.DuplicateTabCommand.Execute(docVm);

        Assert.Equal(2, shell.Documents.Count);
        var copy = Assert.IsType<DocumentViewModel>(shell.Documents[1]);
        Assert.NotSame(docVm, copy);
        Assert.Same(copy, shell.SelectedDocument);
        Assert.True(copy.IsImageLoaded);
        Assert.Equal(docVm.Title + " (copy)", copy.Title);
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

        var shell = CreateShell(settings, fakeDialogs);
        // Swap doc factory for this specific test
        var customShell = new ShellViewModel(
            () => docVm,
            () => throw new InvalidOperationException("Should not create uncrop tab directly"),
            fakeDialogs,
            settings,
            new FakeDownscaleService(),
            new FakeFileLogService(),
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            new BackgroundImageRemover.Services.Strategies.OnnxStrategy(new BackgroundImageRemover.Services.Onnx.OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
            new BackgroundImageRemover.Services.Strategies.GrabCutStrategy(),
            new BackgroundImageRemover.Services.Strategies.SamStrategy(new BackgroundImageRemover.Services.Sam.SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService(),
            new FakeImageLoaderService(),
            new FakeImageExportService());

        await customShell.NewProjectCommand.ExecuteAsync(null);

        Assert.Single(customShell.Documents);
        Assert.Same(docVm, customShell.SelectedDocument);
    }

    private sealed class FakeTab : IDocumentTab
    {
        public string Title { get; set; } = "Tab";
        public string TabTitle => Title;
        public string WindowTitle => Title;
        public bool IsDirty { get; set; }
        public string? DirtyHint => null;
        public bool IsCutout { get; set; }
        public string? CutoutHint => null;
        public Task<bool> TrySaveProjectAsync() => Task.FromResult(true);
#pragma warning disable CS0067
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        public void Dispose() { }
    }

    [Fact]
    public void NextTab_SelectsFollowingTabAndWrapsToFirst()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab1 = new FakeTab { Title = "A" };
        var tab2 = new FakeTab { Title = "B" };
        var tab3 = new FakeTab { Title = "C" };
        shell.Documents.Add(tab1);
        shell.Documents.Add(tab2);
        shell.Documents.Add(tab3);
        shell.SelectedDocument = tab3;

        shell.NextTabCommand.Execute(null);

        Assert.Same(tab1, shell.SelectedDocument);
    }

    [Fact]
    public void PreviousTab_SelectsPrecedingTabAndWrapsToLast()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab1 = new FakeTab { Title = "A" };
        var tab2 = new FakeTab { Title = "B" };
        var tab3 = new FakeTab { Title = "C" };
        shell.Documents.Add(tab1);
        shell.Documents.Add(tab2);
        shell.Documents.Add(tab3);
        shell.SelectedDocument = tab1;

        shell.PreviousTabCommand.Execute(null);

        Assert.Same(tab3, shell.SelectedDocument);
    }

    [Fact]
    public void TabCycling_DoesNothingWithSingleOrNoSelection()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab = new FakeTab { Title = "A" };
        shell.Documents.Add(tab);
        shell.SelectedDocument = tab;

        shell.NextTabCommand.Execute(null);
        shell.PreviousTabCommand.Execute(null);
        Assert.Same(tab, shell.SelectedDocument);

        var emptyShell = CreateShell(new FakeSettingsService());
        emptyShell.Documents.Add(new FakeTab { Title = "B" });
        emptyShell.NextTabCommand.Execute(null);
        Assert.Null(emptyShell.SelectedDocument);
    }

    private sealed class FakeImageDialogService : IDialogService
    {
        private readonly string? _chosenPath;
        public FakeImageDialogService(string? chosenPath) => _chosenPath = chosenPath;

        public string? ShowOpenImageDialog() => _chosenPath;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => null;
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG") => null;
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public BackgroundImageRemover.Models.BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
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

        public Task ExportJpgAsync(OpenCvSharp.Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default)
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
        public Task RunAsync(IReadOnlyList<string> filePaths, BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy strategy, BackgroundImageRemover.Services.Strategies.StrategyContext context, string outputFolder, IProgress<BackgroundImageRemover.Services.Batch.BatchProgress>? progress = null, CancellationToken ct = default, BackgroundImageRemover.Models.BatchExportOptions? exportOptions = null)
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
