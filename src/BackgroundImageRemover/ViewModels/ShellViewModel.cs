using System.Collections.ObjectModel;
using System.Linq;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Top-level window state: the open document tabs and recent-files list.</summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly Func<DocumentViewModel> _documentFactory;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    public ObservableCollection<DocumentViewModel> Documents { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<string> RecentWorkFiles { get; } = new();

    [ObservableProperty]
    private DocumentViewModel? _selectedDocument;

    public ShellViewModel(Func<DocumentViewModel> documentFactory, IDialogService dialogs, ISettingsService settings)
    {
        _documentFactory = documentFactory;
        _dialogs = dialogs;
        _settings = settings;

        foreach (var recent in _settings.Current.RecentFiles)
        {
            RecentFiles.Add(recent);
        }
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

    /// <summary>Creates a fresh, empty project tab (no image yet).</summary>
    [RelayCommand]
    private void NewProject()
    {
        var document = _documentFactory();
        Documents.Add(document);
        SelectedDocument = document;
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

    public async Task OpenInNewTabAsync(string path)
    {
        var document = _documentFactory();
        Documents.Add(document);
        SelectedDocument = document;
        await document.LoadAsync(path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private async Task CloseTabAsync(DocumentViewModel document)
    {
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

    private async Task<bool> ConfirmCloseAsync(DocumentViewModel document)
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

    /// <summary>Asks about each dirty document; returns false when the close should be cancelled.</summary>
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

    public void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var recent in _settings.Current.RecentFiles)
        {
            RecentFiles.Add(recent);
        }
    }

    public void RefreshRecentWorkFiles()
    {
        RecentWorkFiles.Clear();
        foreach (var work in _settings.Current.RecentWorkFiles)
        {
            RecentWorkFiles.Add(work);
        }
    }
}
