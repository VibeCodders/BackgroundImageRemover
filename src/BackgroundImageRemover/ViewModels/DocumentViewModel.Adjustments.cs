using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    // --- Image Adjustments (Brightness, Contrast, Saturation, Hue, Temperature, Tint, Vignette, Blur, Sharpen) ---
    [ObservableProperty]
    private double _adjBrightness = 0.0;

    [ObservableProperty]
    private double _adjContrast = 1.0;

    [ObservableProperty]
    private double _adjSaturation = 1.0;

    [ObservableProperty]
    private double _adjHueShift = 0.0;

    [ObservableProperty]
    private double _adjTemperature = 0.0;

    [ObservableProperty]
    private double _adjTint = 0.0;

    [ObservableProperty]
    private double _adjVignette = 0.0;

    [ObservableProperty]
    private int _adjBlurRadius = 0;

    [ObservableProperty]
    private double _adjSharpenStrength = 0.0;

    [RelayCommand]
    private void ResetAdjustments()
    {
        AdjBrightness = 0.0;
        AdjContrast = 1.0;
        AdjSaturation = 1.0;
        AdjHueShift = 0.0;
        AdjTemperature = 0.0;
        AdjTint = 0.0;
        AdjVignette = 0.0;
        AdjBlurRadius = 0;
        AdjSharpenStrength = 0.0;
    }

    [RelayCommand]
    private async Task ApplyAdjustmentsAsync()
    {
        if (_loadedImage is null)
        {
            StatusMessage = "No image loaded to adjust.";
            return;
        }

        var adjustments = new ImageAdjustments
        {
            Brightness = Math.Clamp(AdjBrightness, -100, 100),
            Contrast = Math.Clamp(AdjContrast, 0.1, 3.0),
            Saturation = Math.Clamp(AdjSaturation, 0.0, 3.0),
            HueShift = Math.Clamp(AdjHueShift, -180, 180),
            Temperature = Math.Clamp(AdjTemperature, -100, 100),
            Tint = Math.Clamp(AdjTint, -100, 100),
            Vignette = Math.Clamp(AdjVignette, 0.0, 1.0),
            BlurRadius = Math.Clamp(AdjBlurRadius, 0, 50),
            SharpenStrength = Math.Clamp(AdjSharpenStrength, 0.0, 3.0)
        };

        if (adjustments.IsIdentity)
        {
            StatusMessage = "No adjustment values changed.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Applying visual adjustments...";

            // Apply the adjustments to the current visible state (the working result when
            // edits exist, otherwise the loaded image) and route them through the standard
            // history-aware pipeline used by every edit tool. This makes the operation
            // undoable and never rewrites the source image or drops its alpha channel.
            using var fallbackAlpha = (_workingBgr is null || _workingAlpha is null) && _loadedImage.FullAlpha is null
                ? new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255))
                : null;
            // Snapshot the inputs on the UI thread: the worker must never read Mats the UI
            // can dispose mid-run (undo/redo stays enabled while busy, and loading a new
            // image replaces the loaded state). Previously the live _workingBgr/_loadedImage
            // refs were handed to the worker, which surfaced as "Cannot access a disposed
            // object" if the user hit undo while adjustments were computing.
            using var sourceBgr = (_workingBgr ?? _loadedImage.FullBgr).Clone();
            using var sourceAlpha = (_workingAlpha ?? _loadedImage.FullAlpha ?? fallbackAlpha!).Clone();

            var adjustedBgr = await Task.Run(() => ImageProcessingHelper.ApplyAdjustments(sourceBgr, adjustments));

            ApplyToolResult(adjustedBgr, sourceAlpha.Clone(), "Adjustments");

            ResetAdjustments();
            StatusMessage = "Image adjustments applied successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Adjustment failed: {ex.Message}";
            _log.Error("Visual adjustments failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
