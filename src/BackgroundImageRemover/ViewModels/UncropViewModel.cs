using System.Windows.Media.Imaging;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Outpaint;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// State and logic for the standalone Uncrop (canvas outpainting) window. Independent of
/// <see cref="DocumentViewModel"/>/the background-removal strategy pipeline: it can be opened
/// empty (with its own "Open image...") or seeded with a document's current image.
/// </summary>
public partial class UncropViewModel : ObservableObject, IDocumentTab
{
    private readonly IUncropFillService _fillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly UncropResultSession _resultSession;
    private CancellationTokenSource? _fillCts;

    private LoadedImage? _sourceImage;

    public UncropOptionsViewModel Options { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private string _title = "Uncrop";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirtyHint))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(TabTitle))]
    private bool _isDirty;

    public string? DirtyHint => IsDirty ? "Unsaved changes — export or save your result." : null;
    public bool IsCutout => false;
    public string? CutoutHint => null;
    public string WindowTitle => Title + (IsDirty ? " *" : string.Empty) + " — Background Image Remover";
    public string TabTitle => IsDirty ? Title + " *" : Title;

    /// <summary>
    /// "Save" for an uncrop document means exporting the result to a PNG. Returns false when
    /// the user cancels the save dialog or the export fails, so the caller knows not to close.
    /// </summary>
    public async Task<bool> TrySaveProjectAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        await _resultSession.SaveAsync();
        return !IsDirty;
    }

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelFillCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenImageCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private BitmapSource? _previewResult;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    public UncropViewModel(
        IUncropFillService fillService,
        IDialogService dialogs,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IFileLogService log)
    {
        _fillService = fillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;

        _resultSession = new UncropResultSession(
            () => _sourceImage,
            _dialogs,
            _imageExporter,
            _log,
            v => IsBusy = v,
            v => IsDirty = v,
            v => StatusMessage = v);

        Options.ImageSizeProvider = () => _sourceImage?.FullBgr.Size();
        Options.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UncropOptionsViewModel.Padding) or nameof(UncropOptionsViewModel.SelectedFillMode))
            {
                ApplyFillCommand.NotifyCanExecuteChanged();
            }
        };
    }
}
