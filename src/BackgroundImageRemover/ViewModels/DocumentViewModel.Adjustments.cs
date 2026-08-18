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
            Brightness = AdjBrightness,
            Contrast = AdjContrast,
            Saturation = AdjSaturation,
            HueShift = AdjHueShift,
            Temperature = AdjTemperature,
            Tint = AdjTint,
            Vignette = AdjVignette,
            BlurRadius = AdjBlurRadius,
            SharpenStrength = AdjSharpenStrength
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
            Mat targetBgr = _workingBgr ?? _loadedImage.FullBgr;
            Mat targetAlpha = _workingAlpha ?? _loadedImage.FullAlpha ?? fallbackAlpha!;

            var adjustedBgr = await Task.Run(() => ImageProcessingHelper.ApplyAdjustments(targetBgr, adjustments));

            ApplyToolResult(adjustedBgr, targetAlpha.Clone(), "Adjustments");

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
