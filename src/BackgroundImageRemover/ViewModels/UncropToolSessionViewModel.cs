using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Outpaint;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for Uncrop / Canvas Expansion outpainting.
/// </summary>
public partial class UncropToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IUncropFillService _fillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly UncropResultSession _resultSession;
    private CancellationTokenSource? _fillCts;

    private LoadedImage? _sourceImage;

    public override string ToolBadge => "⤢ Uncrop";
    public override string AccentColor => "#0078D7";

    public UncropOptionsViewModel Options { get; } = new();

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyFillCommand))]
    private bool _isImageLoaded;

    private readonly BusyGate _busyGate = new();

    /// <summary>True while a fill/apply is in flight; gated commands (ApplyFill, SaveAs) are
    /// disabled and re-evaluated automatically by the gate on every flip.</summary>
    public bool IsBusy
    {
        get => _busyGate.IsBusy;
        set => _busyGate.SetBusy(value);
    }

    [ObservableProperty]
    private BitmapSource? _previewResult;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    public UncropToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IUncropFillService fillService,
        IDialogService dialogs,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IFileLogService log)
        : base(shell, parentDocument)
    {
        _fillService = fillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;

        // The busy overlay binds IsBusy, and the Cancel button must stay enabled while busy:
        // re-raise the property and re-evaluate the tracked cancel command on every flip.
        _busyGate.BusyChanged += value => OnPropertyChanged(nameof(IsBusy));
        _busyGate.Track(CancelFillCommand);

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

        var sourceSnapshot = parentDocument.CreateCurrentStateSnapshot();
        AdoptImage(sourceSnapshot);
    }
}
