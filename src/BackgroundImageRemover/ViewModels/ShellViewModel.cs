using System.Collections.ObjectModel;
using System.Linq;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Top-level window state: the open document tabs and recent-files list.</summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly Func<DocumentViewModel> _documentFactory;
    private readonly Func<UncropViewModel> _uncropFactory;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    public ObservableCollection<IDocumentTab> Documents { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    [ObservableProperty]
    private IDocumentTab? _selectedDocument;

    public ShellViewModel(
        Func<DocumentViewModel> documentFactory,
        Func<UncropViewModel> uncropFactory,
        IDialogService dialogs,
        ISettingsService settings)
    {
        _documentFactory = documentFactory;
        _uncropFactory = uncropFactory;
        _dialogs = dialogs;
        _settings = settings;

        SyncFrom(RecentFiles, _settings.Current.RecentFiles);
        SyncFrom(RecentProjects, _settings.Current.RecentProjects);
    }

    /// <summary>Creates a new Uncrop tab, seeding it with the current document's image
    /// (a clone, so Uncrop's own lifecycle never touches the source document's Mats) when one is loaded.</summary>
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

    /// <summary>Prompts the user to choose between Background Remover and Uncrop.</summary>
    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var (type, openImageImmediately) = _dialogs.ShowNewProjectDialog();
        if (type is null)
        {
            return;
        }

        string? imagePath = null;
        if (openImageImmediately)
        {
            imagePath = _dialogs.ShowOpenImageDialog();
        }

        if (type == Models.NewProjectType.Uncrop)
        {
            var uncropDoc = _uncropFactory();
            Documents.Add(uncropDoc);
            SelectedDocument = uncropDoc;
            if (imagePath is not null)
            {
                await uncropDoc.LoadAsync(imagePath);
                RefreshRecentFiles();
            }
        }
        else
        {
            var document = _documentFactory();
            Documents.Add(document);
            SelectedDocument = document;
            if (imagePath is not null)
            {
                await document.LoadAsync(imagePath);
                RefreshRecentFiles();
                RefreshRecentProjects();
            }
        }
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
        RefreshRecentProjects();
    }

    [RelayCommand]
    private async Task CloseTabAsync(IDocumentTab document)
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

    private async Task<bool> ConfirmCloseAsync(IDocumentTab document)
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
