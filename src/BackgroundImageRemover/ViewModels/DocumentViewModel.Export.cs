using System.IO;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
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
    private bool _isColorPickerOpen;

    private bool CanExport() => IsImageLoaded && !IsBusy
        && (HasWorkingResult || IsSelectedStrategyReady());

    private bool IsSelectedStrategyReady() => SelectedStrategy switch
    {
        StrategyKind.GrabCut => GrabCut.HasValidRect || ScribbleManager.HasScribbles,
        StrategyKind.Onnx => Onnx.IsModelReady,
        StrategyKind.Sam => Sam.IsModelReady && Sam.HasClickedPoint,
        _ => true
    };

    /// <summary>Exports the full-size cutout without cropping (transparent margins kept).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync() => ExportCoreAsync(crop: false);

    /// <summary>Exports the cutout trimmed to the subject (transparent borders removed).</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCroppedAsync() => ExportCoreAsync(crop: true);

    /// <summary>
    /// The single "complete the job" action: computes the full-resolution cutout (faithful
    /// to the preview) when needed, then exports it, optionally trimming transparent borders.
    /// Loaded cutouts / hand-edited results are exported as-is.
    /// </summary>
    private async Task ExportCoreAsync(bool crop)
    {
        if (!await EnsureWorkingResultAsync() || _workingBgr is null || _workingAlpha is null)
        {
            return;
        }

        var baseName = _loadedImage is not null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath)
            : "cutout";
        var suggested = crop ? baseName + "_cropped.png" : baseName + "_cutout.png";

        var path = _dialogs.ShowSavePngDialog(suggested);
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

            switch (ExportBackgroundMode)
            {
                case ExportBackgroundMode.Transparent:
                    await _imageExporter.ExportPngAsync(exportBgra, path);
                    break;

                case ExportBackgroundMode.SolidColor:
                {
                    var colorBgr = new Vec3b(ExportSolidColor.B, ExportSolidColor.G, ExportSolidColor.R);
                    using var composited = BackgroundCompositingService.CompositeOntoColor(exportBgra, colorBgr);
                    await ExportBgrAsPngAsync(composited, path);
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
                    using var composited = BackgroundCompositingService.CompositeOntoImage(exportBgra, background.FullBgr);
                    await ExportBgrAsPngAsync(composited, path);
                    break;
                }
            }

            StatusMessage = $"Exported to {path}";
            _log.Info($"Exported to {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _log.Error("Export failed", ex);
        }
    }

    private async Task ExportBgrAsPngAsync(Mat bgr, string path)
    {
        using var bgra = bgr.ToBgra();
        await _imageExporter.ExportPngAsync(bgra, path);
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
