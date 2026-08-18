using System.Collections.ObjectModel;
using System.Linq;
using BackgroundImageRemover.Services.Dialogs;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

public partial class ShellViewModel
{
    /// <summary>Creates a new standalone Uncrop tab.</summary>
    [RelayCommand]
    private void OpenUncrop()
    {
        var uncropDoc = _uncropFactory();
        if (SelectedDocument is DocumentViewModel doc && doc.LoadedImageForUncrop is { } image)
        {
            uncropDoc.LoadInitialImage(image.Clone());
        }
        Documents.Add(uncropDoc);
        SelectedDocument = uncropDoc;
    }

    [RelayCommand]
    private async Task NewTabAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }
        await OpenInNewTabAsync(path);
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var imagePath = _dialogs.ShowOpenImageDialog();
        if (imagePath is null)
        {
            return;
        }

        await OpenInNewTabAsync(imagePath);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var path = _dialogs.ShowOpenProjectDialog();
        if (path is null)
        {
            return;
        }
        await OpenInNewTabAsync(path);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
    {
        await OpenInNewTabAsync(path);
    }

    /// <summary>Opens a copy of the current tab showing the same state, with a clean history.</summary>
    [RelayCommand]
    private async Task DuplicateTabAsync(IDocumentTab? document)
    {
        if (document is not DocumentViewModel doc || !doc.IsImageLoaded)
        {
            return;
        }

        var copy = _documentFactory();
        copy.SetShell(this);
        Documents.Add(copy);
        SelectedDocument = copy;

        var snapshot = doc.CreateCurrentStateSnapshot();
        await copy.LoadFromSnapshotAsync(snapshot, doc.Title + " (copy)");
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        _settings.ClearRecentFiles();
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void ClearRecentProjects()
    {
        _settings.ClearRecentProjects();
        RefreshRecentProjects();
    }

    /// <summary>Selects the tab after the current one (Ctrl+Tab), wrapping around.</summary>
    [RelayCommand]
    private void NextTab()
    {
        if (Documents.Count < 2 || SelectedDocument is null)
        {
            return;
        }
        int index = Documents.IndexOf(SelectedDocument);
        SelectedDocument = Documents[(index + 1) % Documents.Count];
    }

    /// <summary>Selects the tab before the current one (Ctrl+Shift+Tab), wrapping around.</summary>
    [RelayCommand]
    private void PreviousTab()
    {
        if (Documents.Count < 2 || SelectedDocument is null)
        {
            return;
        }
        int index = Documents.IndexOf(SelectedDocument);
        SelectedDocument = Documents[(index - 1 + Documents.Count) % Documents.Count];
    }

    public async Task OpenInNewTabAsync(string path)
    {
        var document = _documentFactory();
        document.SetShell(this);
        Documents.Add(document);
        SelectedDocument = document;
        await document.LoadAsync(path);
        RefreshRecentFiles();
        RefreshRecentProjects();
    }

    [RelayCommand]
    private async Task CloseTabAsync(IDocumentTab document)
    {
        if (document is IToolSessionTab toolTab)
        {
            toolTab.Cancel();
            return;
        }

        if (document is DocumentViewModel parentDoc && parentDoc.ActiveToolSession is { } activeSession)
        {
            // Close the child tool session first
            CloseTabDirect(activeSession);
        }

        if (!await ConfirmCloseAsync(document))
        {
            return;
        }

        int index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }
        Documents.RemoveAt(index);
        document.Dispose();

        if (SelectedDocument == document)
        {
            SelectedDocument = Documents.Count == 0 ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

    /// <summary>
    /// Prompts the user for a dirty document (Save / Discard / Cancel) and returns true when
    /// it is safe to proceed (discarded or successfully saved).
    /// </summary>
    public async Task<bool> ConfirmCloseAsync(IDocumentTab document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        var choice = _dialogs.ConfirmCloseDocument(document.Title);
        return choice switch
        {
            CloseDocumentResult.Cancel => false,
            CloseDocumentResult.Save => await document.TrySaveProjectAsync(),
            _ => true // Discard
        };
    }

    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var document in Documents.ToList())
        {
            if (!await ConfirmCloseAsync(document))
            {
                return false;
            }
        }
        return true;
    }

    public void RefreshRecentFiles() => SyncFrom(RecentFiles, _settings.Current.RecentFiles);

    public void RefreshRecentProjects() => SyncFrom(RecentProjects, _settings.Current.RecentProjects);

    private static void SyncFrom(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
