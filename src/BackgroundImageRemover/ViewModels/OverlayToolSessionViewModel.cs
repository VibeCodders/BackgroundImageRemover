using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.ImageIo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for compositing a second image (logo/sticker) over the document.</summary>
public partial class OverlayToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;
    private Mat? _overlayBgra;

    public override string ToolBadge => "🔲 Overlay";
    public override string AccentColor => "#059669";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private string? _overlayPath;

    [ObservableProperty]
    private double _scale = 0.25;

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private TextAnchor _anchor = TextAnchor.BottomRight;

    [ObservableProperty]
    private int _margin = 20;

    [ObservableProperty]
    private string? _statusMessage;

    public OverlayToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IDialogService dialogs,
        IImageLoaderService imageLoader)
        : base(shell, parentDocument)
    {
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        RefreshResult();
        StatusMessage = "Choose an overlay image (logo or sticker).";
    }

    partial void OnScaleChanged(double value) => RefreshResult();
    partial void OnOpacityChanged(double value) => RefreshResult();
    partial void OnAnchorChanged(TextAnchor value) => RefreshResult();
    partial void OnMarginChanged(int value) => RefreshResult();

    [RelayCommand]
    private void PickOverlay()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        var loaded = _imageLoader.LoadAsync(path).GetAwaiter().GetResult();
        _overlayBgra?.Dispose();
        using var alpha = loaded.FullAlpha?.Clone()
            ?? new Mat(loaded.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _overlayBgra = loaded.FullBgr.ToBgra(alpha);
        loaded.Dispose();
        OverlayPath = path;
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        if (_overlayBgra is null)
        {
            ResultBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
            IsDirty = false;
            return;
        }

        using var composited = OverlayService.Composite(_sourceImage.FullBgr, _overlayBgra, Anchor, Scale, Opacity, Margin);
        ResultBitmap = composited.ToBitmapSource(_workingAlpha);
        IsDirty = true;
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null || _overlayBgra is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        var bgr = OverlayService.Composite(_sourceImage.FullBgr, _overlayBgra, Anchor, Scale, Opacity, Margin);
        _parentDocument.ApplyToolResult(bgr, _workingAlpha.Clone(), "Overlay");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
        _overlayBgra?.Dispose();
    }
}
