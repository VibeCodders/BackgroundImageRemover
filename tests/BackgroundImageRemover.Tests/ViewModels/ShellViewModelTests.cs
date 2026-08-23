using System.IO;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.ViewModels;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.ViewModels;

public class ShellViewModelTests
{
    private sealed class UnusedDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => throw new NotImplementedException();
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => throw new NotImplementedException();
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
        using var dir = new TempDir();
        var file = dir.CreateFile("new.png");
        var settings = new FakeSettingsService();
        var shell = CreateShell(settings);
        Assert.Empty(shell.RecentFiles);

        settings.AddRecentFile(file);
        shell.RefreshRecentFiles();

        Assert.Equal(new[] { file }, shell.RecentFiles);
    }

    [Fact]
    public void RefreshRecentProjects_ReplacesContentsWithCurrentSettingsState()
    {
        using var dir = new TempDir();
        var project = dir.CreateFile("new.ibrproj");
        var settings = new FakeSettingsService();
        var shell = CreateShell(settings);
        Assert.Empty(shell.RecentProjects);

        settings.AddRecentProject(project);
        shell.RefreshRecentProjects();

        Assert.Equal(new[] { project }, shell.RecentProjects);
    }

    [Fact]
    public void RefreshRecentFiles_DoesNotDuplicateEntries_WhenCalledRepeatedly()
    {
        using var dir = new TempDir();
        var only = dir.CreateFile("only.png");
        var settings = new FakeSettingsService();
        settings.Current.RecentFiles.Add(only);
        var shell = CreateShell(settings);

        shell.RefreshRecentFiles();
        shell.RefreshRecentFiles();

        Assert.Equal(new[] { only }, shell.RecentFiles);
    }

    [Fact]
    public void RefreshRecentFiles_HidesEntriesWhoseFilesNoLongerExist()
    {
        using var dir = new TempDir();
        var existing = dir.CreateFile("exists.png");
        var missing = Path.Combine(dir.Path, "deleted.png"); // never created

        var settings = new FakeSettingsService();
        settings.Current.RecentFiles.Add(missing);
        settings.Current.RecentFiles.Add(existing);
        var shell = CreateShell(settings);

        shell.RefreshRecentFiles();

        // A deleted file must not show up: opening it would spawn a tab that immediately
        // fails to load and then silently disappears.
        Assert.Equal(new[] { existing }, shell.RecentFiles);
    }

    [Fact]
    public void RefreshRecentProjects_HidesEntriesWhoseFilesNoLongerExist()
    {
        using var dir = new TempDir();
        var existing = dir.CreateFile("project.ibrproj");
        var missing = Path.Combine(dir.Path, "deleted.ibrproj"); // never created

        var settings = new FakeSettingsService();
        settings.Current.RecentProjects.Add(missing);
        settings.Current.RecentProjects.Add(existing);
        var shell = CreateShell(settings);

        shell.RefreshRecentProjects();

        Assert.Equal(new[] { existing }, shell.RecentProjects);
    }

    [Fact]
    public void ClearRecentFiles_EmptiesBothListAndSettings()
    {
        using var dir = new TempDir();
        var settings = new FakeSettingsService();
        settings.AddRecentFile(dir.CreateFile("a.png"));
        settings.AddRecentFile(dir.CreateFile("b.png"));
        settings.AddRecentProject(dir.CreateFile("p.ibrproj"));
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

    [Fact]
    public async Task OpenInNewTabAsync_AlreadyOpenFile_FocusesExistingTabInsteadOfDuplicating()
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

        await shell.OpenInNewTabAsync("photo.png");
        var first = Assert.Single(shell.Documents);

        await shell.OpenInNewTabAsync("PHOTO.PNG"); // different case must still match

        Assert.Single(shell.Documents);
        Assert.Same(first, shell.Documents[0]);
        Assert.Same(first, shell.SelectedDocument);
    }

    [Fact]
    public async Task OpenInNewTabAsync_FailedLoad_DoesNotLeaveDeadTabBehind()
    {
        var settings = new FakeSettingsService();
        var fakeDialogs = new FakeImageDialogService("bad.png");

        Func<DocumentViewModel> docFactory = () => new DocumentViewModel(
            new FailingImageLoaderService(),
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

        await shell.OpenInNewTabAsync("bad.png");

        Assert.Empty(shell.Documents);
        Assert.Null(shell.SelectedDocument);
    }

    [Fact]
    public async Task CloseOtherTabs_ClosesEveryTabExceptTheKeptOne()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab1 = new FakeTab { Title = "A" };
        var tab2 = new FakeTab { Title = "B" };
        var tab3 = new FakeTab { Title = "C" };
        shell.Documents.Add(tab1);
        shell.Documents.Add(tab2);
        shell.Documents.Add(tab3);
        shell.SelectedDocument = tab1;

        await shell.CloseOtherTabsCommand.ExecuteAsync(tab2);

        Assert.Single(shell.Documents);
        Assert.Same(tab2, shell.Documents[0]);
    }

    [Fact]
    public async Task CloseOtherTabs_DoesNothingWithoutATargetTab()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab = new FakeTab { Title = "A" };
        shell.Documents.Add(tab);

        await shell.CloseOtherTabsCommand.ExecuteAsync(null);

        Assert.Single(shell.Documents);
    }

    [Fact]
    public async Task CloseOtherTabs_AbortsAndKeepsTheRest_WhenUserCancelsDirtyPrompt()
    {
        var shell = CreateShell(new FakeSettingsService(), new CancelCloseDialogService());
        var dirty = new FakeTab { Title = "Dirty", IsDirty = true };
        var kept = new FakeTab { Title = "Kept" };
        var clean = new FakeTab { Title = "Clean" };
        shell.Documents.Add(dirty);
        shell.Documents.Add(kept);
        shell.Documents.Add(clean);

        await shell.CloseOtherTabsCommand.ExecuteAsync(kept);

        // The dirty tab prompts first and cancels, so no tab is closed at all.
        Assert.Equal(3, shell.Documents.Count);
    }

    [Fact]
    public async Task CloseAllTabs_ClosesEverythingAndClearsSelection()
    {
        var shell = CreateShell(new FakeSettingsService());
        var tab1 = new FakeTab { Title = "A" };
        var tab2 = new FakeTab { Title = "B" };
        shell.Documents.Add(tab1);
        shell.Documents.Add(tab2);
        shell.SelectedDocument = tab1;

        await shell.CloseAllTabsCommand.ExecuteAsync(null);

        Assert.Empty(shell.Documents);
        Assert.Null(shell.SelectedDocument);
    }

    [Fact]
    public async Task CloseAllTabs_KeepsEverythingWhenUserCancelsDirtyPrompt()
    {
        var shell = CreateShell(new FakeSettingsService(), new CancelCloseDialogService());
        var dirty = new FakeTab { Title = "Dirty", IsDirty = true };
        var clean = new FakeTab { Title = "Clean" };
        shell.Documents.Add(dirty);
        shell.Documents.Add(clean);
        shell.SelectedDocument = dirty;

        await shell.CloseAllTabsCommand.ExecuteAsync(null);

        Assert.Equal(2, shell.Documents.Count);
        Assert.Same(dirty, shell.SelectedDocument);
    }

    /// <summary>Creates a disposable temp folder with helper to create files inside it, so
    /// recent-menu tests exercise the real on-disk existence filter.</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"recent_tests_{Guid.NewGuid():N}");

        public TempDir() => Directory.CreateDirectory(Path);

        public string CreateFile(string name)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, "x");
            return full;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private sealed class FailingImageLoaderService : BackgroundImageRemover.Services.ImageIo.IImageLoaderService
    {
        public Task<Models.LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated decode failure");

        public Task<Models.LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => throw new InvalidOperationException("simulated decode failure");

        public Task<Models.LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => throw new InvalidOperationException("simulated decode failure");
    }

    private sealed class CancelCloseDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => throw new NotImplementedException();
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowOpenProjectDialog() => throw new NotImplementedException();
        public string? ShowSaveProjectDialog(string? suggestedFileName) => throw new NotImplementedException();
        public BackgroundImageRemover.Models.BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Cancel;
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private sealed class FakeImageDialogService : IDialogService
    {
        private readonly string? _chosenPath;
        public FakeImageDialogService(string? chosenPath) => _chosenPath = chosenPath;

        public string? ShowOpenImageDialog() => _chosenPath;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => null;
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => null;
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => null;
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public BackgroundImageRemover.Models.BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private sealed class FakeImageLoaderService : BackgroundImageRemover.Services.ImageIo.IImageLoaderService
    {
        public Task<Models.LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new Models.LoadedImage(path, new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)));

        public Task<Models.LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new Models.LoadedImage(sourceName, new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)));

        public Task<Models.LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new Models.LoadedImage(sourceName, new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)));
    }

}
