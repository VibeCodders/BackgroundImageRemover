using System.Collections.ObjectModel;
using System.IO;
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

    /// <summary>Opens the Preferences dialog (theme, language, startup behavior).</summary>
    [RelayCommand]
    private void ShowPreferences() => _dialogs.ShowPreferencesDialog();

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

    /// <summary>
    /// Opens a file or project in a new tab. When <paramref name="displayTitle"/> is provided
    /// (crash-recovery restore), the tab keeps its original name, is marked dirty, and is not
    /// bound to the recovery file — the next Ctrl+S prompts for a real save location.
    /// </summary>
    public async Task OpenInNewTabAsync(string path, string? displayTitle = null)
    {
        // Reopening a file that is already open just focuses its tab instead of stacking a
        // duplicate. Crash-recovery restores bypass this because they rebind old titles.
        if (displayTitle is null)
        {
            var existing = Documents.OfType<DocumentViewModel>()
                .FirstOrDefault(d => d.IsImageLoaded
                    && string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                SelectedDocument = existing;
                return;
            }
        }

        var document = _documentFactory();
        document.SetShell(this);
        Documents.Add(document);
        SelectedDocument = document;
        await document.LoadAsync(path);
        if (displayTitle is not null)
        {
            document.Title = displayTitle;
            document.ProjectPath = null;
            document.IsDirty = true;
        }

        if (!document.IsImageLoaded)
        {
            // A failed open (corrupt or unsupported file) must not leave a dead tab behind.
            Documents.Remove(document);
            document.Dispose();
            SelectedDocument = Documents.Count == 0 ? null : Documents[^1];
            return;
        }

        RefreshRecentFiles();
        RefreshRecentProjects();
    }

    /// <summary>
    /// Closes one tab (prompting when dirty) and returns whether it was actually closed.
    /// Tool-session tabs are cancelled (which removes them); the parent-document branch also
    /// closes the session first. Returns false when the user cancels a save prompt.
    /// </summary>
    [RelayCommand]
    private async Task<bool> CloseTabAsync(IDocumentTab document)
    {
        if (document is IToolSessionTab toolTab)
        {
            toolTab.Cancel();
            return true;
        }

        if (document is DocumentViewModel parentDoc && parentDoc.ActiveToolSession is { } activeSession)
        {
            // Close the child tool session first
            CloseTabDirect(activeSession);
        }

        if (!await ConfirmCloseAsync(document))
        {
            return false;
        }

        int index = Documents.IndexOf(document);
        if (index < 0)
        {
            return false;
        }
        Documents.RemoveAt(index);
        document.Dispose();

        if (SelectedDocument == document)
        {
            SelectedDocument = Documents.Count == 0 ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
        return true;
    }

    /// <summary>
    /// Closes every tab except the one the context menu was opened on (right-click "Close
    /// others"), prompting for dirty documents. Tool-session tabs are cancelled, and the
    /// whole operation aborts if the user cancels a prompt.
    /// </summary>
    [RelayCommand]
    private async Task CloseOtherTabsAsync(IDocumentTab? keep)
    {
        if (keep is null)
        {
            return;
        }

        foreach (var document in Documents.Where(d => !ReferenceEquals(d, keep)).ToList())
        {
            if (!await CloseTabAsync(document))
            {
                return; // user cancelled a save prompt: keep the rest untouched
            }
        }
    }

    /// <summary>Closes all tabs after confirming every dirty document (right-click "Close all").</summary>
    [RelayCommand]
    private async Task CloseAllTabsAsync()
    {
        if (!await ConfirmCloseAllAsync())
        {
            return;
        }

        foreach (var document in Documents.ToList())
        {
            Documents.Remove(document);
            document.Dispose();
        }
        SelectedDocument = null;
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

    /// <summary>Repopulates the recent-files menu, hiding entries whose files no longer exist
    /// (a deleted file would otherwise open a tab that immediately fails and disappears).</summary>
    public void RefreshRecentFiles() => SyncFrom(RecentFiles, _settings.Current.RecentFiles.Where(File.Exists));

    /// <summary>Repopulates the recent-projects menu, hiding entries whose files no longer exist.</summary>
    public void RefreshRecentProjects() => SyncFrom(RecentProjects, _settings.Current.RecentProjects.Where(File.Exists));

    private static void SyncFrom(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
