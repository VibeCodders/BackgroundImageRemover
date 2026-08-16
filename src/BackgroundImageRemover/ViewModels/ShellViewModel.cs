using System.Collections.ObjectModel;
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
        await document.LoadImageAsync(path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void CloseTab(DocumentViewModel document)
    {
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

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var recent in _settings.Current.RecentFiles)
        {
            RecentFiles.Add(recent);
        }
    }
}
