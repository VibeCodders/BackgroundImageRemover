using System.Diagnostics;
using System.IO;
using System.Windows;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    [ObservableProperty]
    private ExportBackgroundMode _exportBackgroundMode = ExportBackgroundMode.Transparent;

    [ObservableProperty]
    private WpfColor _exportSolidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private string? _exportBackgroundImagePath;

    [ObservableProperty]
    private double _exportBlurRadius = 10;

    [ObservableProperty]
    private WpfColor _exportGradientTopColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private WpfColor _exportGradientBottomColor = WpfColor.FromRgb(120, 120, 120);

    [ObservableProperty]
    private int _exportJpegQuality = 95;

    /// <summary>Path of the most recently exported file (or folder, for a batch), used by the
    /// "Open folder" status-bar action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastExport))]
    private string? _lastExportedFilePath;

    public bool HasLastExport => !string.IsNullOrWhiteSpace(LastExportedFilePath);

    [RelayCommand]
    private void RevealLastExportInExplorer()
    {
        if (LastExportedFilePath is null)
        {
            return;
        }

        if (!ViewInteractionHelper.RevealInExplorer(LastExportedFilePath))
        {
            StatusMessage = "Could not open the folder in Explorer.";
            _log.Error("Could not reveal last export in Explorer", null);
        }
    }

    [ObservableProperty]
    private bool _exportDropShadowEnabled;

    [ObservableProperty]
    private double _exportShadowOffset = 12;

    [ObservableProperty]
    private double _exportShadowBlur = 6;

    [ObservableProperty]
    private double _exportShadowOpacity = 0.45;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientTopColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientBottomColorPickerOpen;

    private bool CanExport() => IsImageLoaded && !IsBusy
        && (HasWorkingResult || IsSelectedStrategyReady());

    private bool IsSelectedStrategyReady() => SelectedStrategy switch
    {
        StrategyKind.GrabCut => GrabCut.HasValidRect || ScribbleManager.HasScribbles,
        StrategyKind.Onnx => Onnx.IsModelReady,
        StrategyKind.Sam => Sam.IsModelReady && Sam.HasClickedPoint,
        StrategyKind.MagicWand => MagicWand.HasClickedPoint,
        _ => true
    };

    /// <summary>Exports the full-size cutout without cropping (transparent margins kept).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync() => ExportCoreAsync(crop: false, ExportFormat.Png);

    /// <summary>Exports the cutout trimmed to the subject (transparent borders removed).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCroppedAsync() => ExportCoreAsync(crop: true, ExportFormat.Png);

    /// <summary>Exports the full-size cutout as a JPEG (composited onto a background, since JPEG has no alpha).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportJpgAsync() => ExportCoreAsync(crop: false, ExportFormat.Jpeg);

    /// <summary>Exports the trimmed cutout as a JPEG (composited onto a background).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportJpgCroppedAsync() => ExportCoreAsync(crop: true, ExportFormat.Jpeg);

    /// <summary>Exports the full-size cutout as a WebP (transparency preserved, smaller than PNG).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportWebpAsync() => ExportCoreAsync(crop: false, ExportFormat.Webp);

    /// <summary>Exports the trimmed cutout as a WebP (transparent borders removed).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportWebpCroppedAsync() => ExportCoreAsync(crop: true, ExportFormat.Webp);

    /// <summary>Copies the full-resolution cutout to the clipboard as a PNG image.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task CopyToClipboardAsync()
    {
        if (!await EnsureWorkingResultAsync() || _workingBgr is null || _workingAlpha is null)
        {
            return;
        }

        try
        {
            using var bgra = _workingBgr.ToBgra(_workingAlpha);
            BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);

            var bitmap = bgra.ToBitmapSource();
            bitmap.Freeze();
            Clipboard.SetImage(bitmap);
            StatusMessage = "Cutout copied to clipboard.";
            _log.Info("Cutout copied to clipboard");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not copy to clipboard: {ex.Message}";
            _log.Error("Could not copy to clipboard", ex);
        }
    }

    private enum ExportFormat
    {
        Png,
        Jpeg,
        Webp
    }

    /// <summary>
    /// The single "complete the job" action: computes the full-resolution cutout (faithful
    /// to the preview) when needed, then exports it, optionally trimming transparent borders.
    /// Loaded cutouts / hand-edited results are exported as-is. JPEG output composites the
    /// cutout onto a background first (JPEG cannot store transparency).
    /// </summary>
    private async Task ExportCoreAsync(bool crop, ExportFormat format)
    {
        if (!await EnsureWorkingResultAsync() || _workingBgr is null || _workingAlpha is null)
        {
            return;
        }

        var baseName = _loadedImage is not null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath)
            : "cutout";
        var extension = format switch
        {
            ExportFormat.Jpeg => ".jpg",
            ExportFormat.Webp => ".webp",
            _ => ".png"
        };
        var suffix = crop ? "_cropped" : "_cutout";
        var suggested = baseName + suffix + extension;

        var path = format switch
        {
            ExportFormat.Jpeg => _dialogs.ShowSaveJpgDialog(suggested),
            ExportFormat.Webp => _dialogs.ShowSaveWebpDialog(suggested),
            _ => _dialogs.ShowSavePngDialog(suggested)
        };
        if (path is null)
        {
            return;
        }

        try
        {
            using var bgra = _workingBgr.ToBgra(_workingAlpha);

            // Fully-removed pixels must not carry the original color data forward: leaving it
            // in place is invisible today, but re-running a strategy (or reopening the file)
            // later reads it back as real image content and can resurrect the old background.
            BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);

            // "Crop" trims the transparent margins so the exported PNG hugs the subject.
            using var cropped = crop ? BackgroundCompositingService.TrimTransparentBorders(bgra) : null;
            var exportBgra = cropped ?? bgra;

            // A drop shadow is baked into a padded, still-transparent canvas before the
            // background is composited, so it works with every background mode.
            using var shadowed = ExportDropShadowEnabled
                ? BackgroundCompositingService.ApplyDropShadow(exportBgra, ExportShadowOffset, ExportShadowOffset, ExportShadowBlur, ExportShadowOpacity)
                : null;
            var subject = shadowed ?? exportBgra;

            switch (ExportBackgroundMode)
            {
                case ExportBackgroundMode.Transparent:
                    if (format == ExportFormat.Jpeg)
                    {
                        // JPEG cannot store alpha: composite the cutout onto white.
                        using var onWhite = BackgroundCompositingService.CompositeOntoColor(subject, new Vec3b(255, 255, 255));
                        await ExportBgrAsJpgAsync(onWhite, path);
                    }
                    else if (format == ExportFormat.Webp)
                    {
                        // WebP stores alpha like PNG but is typically much smaller.
                        await _imageExporter.ExportWebpAsync(subject, path, Math.Clamp(ExportJpegQuality, 1, 100));
                    }
                    else
                    {
                        await _imageExporter.ExportPngAsync(subject, path);
                    }
                    break;

                case ExportBackgroundMode.SolidColor:
                {
                    var colorBgr = new Vec3b(ExportSolidColor.B, ExportSolidColor.G, ExportSolidColor.R);
                    using var composited = BackgroundCompositingService.CompositeOntoColor(subject, colorBgr);
                    await ExportBgrAsAsync(composited, path, format);
                    break;
                }

                case ExportBackgroundMode.Image:
                {
                    if (ExportBackgroundImagePath is null)
                    {
                        StatusMessage = "Choose a background image first.";
                        return;
                    }
                    using var background = await _imageLoader.LoadAsync(ExportBackgroundImagePath);
                    using var composited = BackgroundCompositingService.CompositeOntoImage(subject, background.FullBgr);
                    await ExportBgrAsAsync(composited, path, format);
                    break;
                }

                case ExportBackgroundMode.Blur:
                {
                    if (_loadedImage is null)
                    {
                        StatusMessage = "No source image available for the blur background.";
                        return;
                    }
                    using var composited = BackgroundCompositingService.CompositeOntoBlurredImage(subject, _loadedImage.FullBgr, ExportBlurRadius);
                    await ExportBgrAsAsync(composited, path, format);
                    break;
                }

                case ExportBackgroundMode.Gradient:
                {
                    var top = new Vec3b(ExportGradientTopColor.B, ExportGradientTopColor.G, ExportGradientTopColor.R);
                    var bottom = new Vec3b(ExportGradientBottomColor.B, ExportGradientBottomColor.G, ExportGradientBottomColor.R);
                    using var composited = BackgroundCompositingService.CompositeOntoGradient(subject, top, bottom);
                    await ExportBgrAsAsync(composited, path, format);
                    break;
                }
            }

            LastExportedFilePath = path;
            StatusMessage = $"Exported to {path}";
            _log.Info($"Exported to {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _log.Error("Export failed", ex);
        }
    }

    private async Task ExportBgrAsAsync(Mat bgr, string path, ExportFormat format)
    {
        switch (format)
        {
            case ExportFormat.Jpeg:
                await ExportBgrAsJpgAsync(bgr, path);
                break;
            case ExportFormat.Webp:
                using (var webpBgra = bgr.ToBgra())
                {
                    await _imageExporter.ExportWebpAsync(webpBgra, path, Math.Clamp(ExportJpegQuality, 1, 100));
                }
                break;
            default:
                using (var bgra = bgr.ToBgra())
                {
                    await _imageExporter.ExportPngAsync(bgra, path);
                }
                break;
        }
    }

    private async Task ExportBgrAsJpgAsync(Mat bgr, string path)
    {
        await _imageExporter.ExportJpgAsync(bgr, path, Math.Clamp(ExportJpegQuality, 1, 100));
    }

    [RelayCommand]
    private void PickBackgroundImage()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            ExportBackgroundImagePath = path;
        }
    }
}
