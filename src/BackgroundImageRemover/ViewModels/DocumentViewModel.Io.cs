using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            await LoadAsync(path);
        }
    }

    /// <summary>Loads an image or a <c>.ibrproj</c> project, dispatching on the file extension.</summary>
    public async Task LoadAsync(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".ibrproj", StringComparison.OrdinalIgnoreCase))
        {
            await LoadProjectAsync(path);
        }
        else
        {
            await LoadImageAsync(path);
        }
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var clipboardBitmap = ViewInteractionHelper.TryGetClipboardImage();
        if (clipboardBitmap is null)
        {
            StatusMessage = "No image found in clipboard.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Pasting image from clipboard...";
            var loaded = await _imageLoader.LoadFromBitmapSourceAsync(clipboardBitmap, "Clipboard Image");
            await InitializeLoadedImageAsync(loaded, "Clipboard Image");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not paste image: {ex.Message}";
            _log.Error("Could not paste image from clipboard", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading image...";
            var loaded = await _imageLoader.LoadAsync(path);
            await InitializeLoadedImageAsync(loaded, path);
            _settings.AddRecentFile(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
            _log.Error($"Could not load image: {path}", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeLoadedImageAsync(LoadedImage loaded, string sourceName)
    {
        _previewCts?.Cancel();

        _loadedImage?.Dispose();
        _preview?.Dispose();
        DisposeWorkingResult();
        _history.Clear();
        RefreshUndoRedoState();
        _samEmbedding = null;
        _samPromptPointPreview = null;
        Sam.HasClickedPoint = false;
        _magicWandSeedPreview = null;
        MagicWand.HasClickedPoint = false;

        _loadedImage = loaded;
        var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
        _preview = preview;

        bool isActualCutout = BackgroundCompositingService.HasMeaningfulTransparency(_loadedImage.FullAlpha);
        PreviewBitmap = isActualCutout
            ? preview.Bgr.BuildPreviewWithAlpha(_loadedImage.FullAlpha!)
            : preview.Bgr.ToBitmapSource();
        ResultBitmap = null;
        IsImageLoaded = true;
        IsCutout = isActualCutout;
        var displayTitle = Path.GetFileName(sourceName);
        if (string.IsNullOrWhiteSpace(displayTitle)) displayTitle = sourceName;
        Title = IsCutout ? displayTitle + " (cutout)" : displayTitle;
        StatusMessage = $"Loaded {displayTitle} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
        _log.Info($"Loaded image {sourceName} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})");

        GrabCut.SelectedRect = null;
        ScribbleManager.Clear();
        GrabCut.HasScribbles = false;
        ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);

        if (SelectedStrategy == StrategyKind.Sam && Sam.IsModelReady)
        {
            ComputeSamEmbedding();
        }

        if (isActualCutout)
        {
            AdoptLoadedCutout();
        }
        // Non-cutout images open with no effect applied: the original preview stays visible
        // until the user picks a removal strategy and triggers a preview.
    }
}
