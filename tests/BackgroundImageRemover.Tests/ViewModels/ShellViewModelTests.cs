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

        var fakeUncrop = new FakeUncropTab();
        var shell = new ShellViewModel(
            () => throw new InvalidOperationException("Should not create DocumentViewModel"),
            () => (UncropViewModel)(object)fakeUncrop,
            fakeDialogs,
            settings);

        await shell.NewProjectCommand.ExecuteAsync(null);

        Assert.Single(shell.Documents);
        Assert.Same(fakeUncrop, shell.SelectedDocument);
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

    private sealed class FakeUncropTab : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDocumentTab
    {
        public string Title => "Uncrop";
        public string TabTitle => "Uncrop";
        public string WindowTitle => "Uncrop — Background Image Remover";
        public bool IsDirty => false;
        public string? DirtyHint => null;
        public bool IsCutout => false;
        public string? CutoutHint => null;
        public Task<bool> TrySaveProjectAsync() => Task.FromResult(true);
        public void Dispose() { }
    }
}
