using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
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

    /// <summary>Opens a dropped file in a brand-new tab via the shell (used for multi-file drops).</summary>
    public async Task OpenDroppedFileInNewTabAsync(string path)
    {
        if (_shell is null)
        {
            return;
        }
        await _shell.OpenInNewTabAsync(path);
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

    /// <summary>
    /// Initializes a fresh document from an already-decoded state (used by Duplicate Tab), so
    /// the copy shows exactly the current view of the source tab with a clean edit history.
    /// </summary>
    public async Task LoadFromSnapshotAsync(LoadedImage snapshot, string sourceName)
    {
        IsBusy = true;
        BusyMessage = "Duplicating tab...";
        try
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
            ProjectPath = null;
            GrabCut.SelectedRect = null;
            ScribbleManager.Clear();
            GrabCut.HasScribbles = false;

            _loadedImage = snapshot;
            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;
            PreviewBitmap = preview.Bgr.ToBitmapSource();
            ResultBitmap = null;

            // The snapshot is the current state of the source tab: restore it verbatim as the
            // working result so the duplicate is pixel-identical, even when the source has no
            // meaningful transparency.
            _workingBgr = _loadedImage.FullBgr.Clone();
            _workingAlpha = _loadedImage.FullAlpha?.Clone()
                ?? new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
            _workingResultIsLoadedCutout = false;
            _workingResultHandEdited = true;

            IsImageLoaded = true;
            IsCutout = BackgroundCompositingService.HasMeaningfulTransparency(_workingAlpha);
            ImageWidth = _loadedImage.FullBgr.Width;
            ImageHeight = _loadedImage.FullBgr.Height;
            Title = sourceName;
            StatusMessage = $"Duplicated {sourceName} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";
            _log.Info($"Duplicated tab from {sourceName}");

            OnPropertyChanged(nameof(HasWorkingResult));
            RefreshUndoRedoState();
            ExportCommand.NotifyCanExecuteChanged();
            RefreshResultBitmapFromWorking();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not duplicate tab: {ex.Message}";
            _log.Error("Could not duplicate tab", ex);
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
        ImageWidth = _loadedImage.FullBgr.Width;
        ImageHeight = _loadedImage.FullBgr.Height;
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
