using System.Collections.ObjectModel;
using System.Linq;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
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
    private readonly IDownscaleService _downscaler;
    private readonly IFileLogService _log;
    private readonly IEnumerable<IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;
    private readonly IUncropFillService _uncropFillService;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;

    public ObservableCollection<IDocumentTab> Documents { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    [ObservableProperty]
    private IDocumentTab? _selectedDocument;

    public ShellViewModel(
        Func<DocumentViewModel> documentFactory,
        Func<UncropViewModel> uncropFactory,
        IDialogService dialogs,
        ISettingsService settings,
        IDownscaleService downscaler,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy,
        IUncropFillService uncropFillService,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter)
    {
        _documentFactory = documentFactory;
        _uncropFactory = uncropFactory;
        _dialogs = dialogs;
        _settings = settings;
        _downscaler = downscaler;
        _log = log;
        _strategies = strategies;
        _onnxStrategy = onnxStrategy;
        _grabCutStrategy = grabCutStrategy;
        _samStrategy = samStrategy;
        _uncropFillService = uncropFillService;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;

        SyncFrom(RecentFiles, _settings.Current.RecentFiles);
        SyncFrom(RecentProjects, _settings.Current.RecentProjects);
    }

    /// <summary>
    /// Opens a modal tool session tab for the specified tool.
    /// If a session is already active for this document, focuses it.
    /// </summary>
    public void OpenToolSession(DocumentViewModel doc, EditorTool tool)
    {
        if (doc.ActiveToolSession is { } existingTab)
        {
            SelectedDocument = existingTab;
            return;
        }

        IToolSessionTab? toolTab = tool switch
        {
            EditorTool.RemoveBackground => new BackgroundRemoverToolSessionViewModel(
                this, doc, _downscaler, _dialogs, _log, _strategies, _onnxStrategy, _grabCutStrategy, _samStrategy),
            EditorTool.Uncrop => new UncropToolSessionViewModel(
                this, doc, _uncropFillService, _dialogs, _imageLoader, _imageExporter, _log),
            EditorTool.Retouch => new RetouchToolSessionViewModel(this, doc),
            EditorTool.Adjustments => new AdjustmentsToolSessionViewModel(this, doc, _log),
            _ => null
        };

        if (toolTab is null) return;

        doc.ActiveToolSession = toolTab;

        // Insert tool tab right after parent document
        int parentIdx = Documents.IndexOf(doc);
        if (parentIdx >= 0 && parentIdx + 1 <= Documents.Count)
        {
            Documents.Insert(parentIdx + 1, toolTab);
        }
        else
        {
            Documents.Add(toolTab);
        }

        SelectedDocument = toolTab;
    }

    /// <summary>
    /// Closes a tool session directly without prompting.
    /// </summary>
    public void CloseTabDirect(IToolSessionTab toolTab)
    {
        if (toolTab.ParentDocument is { } parent)
        {
            if (parent.ActiveToolSession == toolTab)
            {
                parent.ActiveToolSession = null;
            }
        }

        int index = Documents.IndexOf(toolTab);
        if (index >= 0)
        {
            Documents.RemoveAt(index);
        }
        toolTab.Dispose();

        if (toolTab.ParentDocument is { } targetDoc && Documents.Contains(targetDoc))
        {
            SelectedDocument = targetDoc;
        }
        else if (SelectedDocument == toolTab)
        {
            SelectedDocument = Documents.Count == 0 ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

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
