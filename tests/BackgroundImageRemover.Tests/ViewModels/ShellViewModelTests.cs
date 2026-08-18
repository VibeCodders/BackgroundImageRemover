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
        public (Models.NewProjectType? Type, bool OpenImageImmediately) ShowNewProjectDialog() => throw new NotImplementedException();
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
    public async Task NewProjectAsync_WhenUncropSelected_AddsUncropTab()
    {
        var settings = new FakeSettingsService();
        var fakeDialogs = new FakeNewProjectDialogService((Models.NewProjectType.Uncrop, false));

        var uncropVm = new UncropViewModel(
            new FakeUncropFillService(),
            fakeDialogs,
            new FakeImageLoaderService(),
            new FakeImageExportService(),
            new FakeFileLogService());

        var shell = new ShellViewModel(
            () => throw new InvalidOperationException("Should not create DocumentViewModel"),
            () => uncropVm,
            fakeDialogs,
            settings);

        await shell.NewProjectCommand.ExecuteAsync(null);

        Assert.Single(shell.Documents);
        Assert.Same(uncropVm, shell.SelectedDocument);
    }

    private sealed class FakeNewProjectDialogService : IDialogService
    {
        private readonly (Models.NewProjectType? Type, bool OpenImageImmediately) _result;
        public FakeNewProjectDialogService((Models.NewProjectType? Type, bool OpenImageImmediately) result) => _result = result;

        public (Models.NewProjectType? Type, bool OpenImageImmediately) ShowNewProjectDialog() => _result;
        public string? ShowOpenImageDialog() => null;
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
        public OpenCvSharp.Mat FillInpaint(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, Models.UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillMirror(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, Models.UncropMirrorType mirrorType = Models.UncropMirrorType.Reflect101, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillSolidColor(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, bool blurred, OpenCvSharp.Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillReplicate(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, CancellationToken ct = default)
            => new(1, 1, OpenCvSharp.MatType.CV_8UC3);
        public OpenCvSharp.Mat FillWrap(OpenCvSharp.Mat sourceBgr, Models.CanvasPadding padding, CancellationToken ct = default)
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
}
