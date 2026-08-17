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
}
