using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for Color Filters, Brightness, Contrast, Saturation, Hue, Blur and Sharpen adjustments.
/// </summary>
public partial class AdjustmentsToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IFileLogService _log;
    private Mat? _workingBgr;

    public override string ToolBadge => "✨ Adjustments";
    public override string AccentColor => "#D97706";

    [ObservableProperty]
    private BitmapSource? _originalBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

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

    [ObservableProperty]
    private double _adjExposure = 1.0;

    [ObservableProperty]
    private double _adjHighlights = 0.0;

    [ObservableProperty]
    private double _adjShadows = 0.0;

    [ObservableProperty]
    private double _adjDenoise = 0.0;

    [ObservableProperty]
    private bool _adjAutoEnhance;

    [ObservableProperty]
    private double _adjVibrance;

    [ObservableProperty]
    private double _adjClarity;

    [ObservableProperty]
    private double _adjFade;

    [ObservableProperty]
    private double _adjGrain;

    [ObservableProperty]
    private double _adjMonochrome;

    [ObservableProperty]
    private double _adjDehaze;

    [ObservableProperty]
    private double _adjSoften;

    [ObservableProperty]
    private double _adjSepiaTone;

    [ObservableProperty]
    private double _adjInvertAmount;

    [ObservableProperty]
    private int _adjPosterizeLevels;

    [ObservableProperty]
    private bool _isCompareMode;

    [ObservableProperty]
    private double _compareDividerPosition = 0.5;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public AdjustmentsToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IFileLogService log)
        : base(shell, parentDocument)
    {
        _log = log;
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgr = _sourceImage!.FullBgr.Clone();

        OriginalBitmap = _workingBgr.ToBitmapSource(_workingAlpha!);
        ResultBitmap = OriginalBitmap;

        StatusMessage = "Adjust sliders to preview visual changes.";
    }

    partial void OnAdjBrightnessChanged(double value) => UpdateLivePreview();
    partial void OnAdjContrastChanged(double value) => UpdateLivePreview();
    partial void OnAdjSaturationChanged(double value) => UpdateLivePreview();
    partial void OnAdjHueShiftChanged(double value) => UpdateLivePreview();
    partial void OnAdjTemperatureChanged(double value) => UpdateLivePreview();
    partial void OnAdjTintChanged(double value) => UpdateLivePreview();
    partial void OnAdjVignetteChanged(double value) => UpdateLivePreview();
    partial void OnAdjBlurRadiusChanged(int value) => UpdateLivePreview();
    partial void OnAdjSharpenStrengthChanged(double value) => UpdateLivePreview();
    partial void OnAdjExposureChanged(double value) => UpdateLivePreview();
    partial void OnAdjHighlightsChanged(double value) => UpdateLivePreview();
    partial void OnAdjShadowsChanged(double value) => UpdateLivePreview();
    partial void OnAdjDenoiseChanged(double value) => UpdateLivePreview();
    partial void OnAdjAutoEnhanceChanged(bool value) => UpdateLivePreview();
    partial void OnAdjVibranceChanged(double value) => UpdateLivePreview();
    partial void OnAdjClarityChanged(double value) => UpdateLivePreview();
    partial void OnAdjFadeChanged(double value) => UpdateLivePreview();
    partial void OnAdjGrainChanged(double value) => UpdateLivePreview();
    partial void OnAdjMonochromeChanged(double value) => UpdateLivePreview();
    partial void OnAdjDehazeChanged(double value) => UpdateLivePreview();
    partial void OnAdjSoftenChanged(double value) => UpdateLivePreview();
    partial void OnAdjSepiaToneChanged(double value) => UpdateLivePreview();
    partial void OnAdjInvertAmountChanged(double value) => UpdateLivePreview();
    partial void OnAdjPosterizeLevelsChanged(int value) => UpdateLivePreview();

    private void UpdateLivePreview()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

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
            SharpenStrength = AdjSharpenStrength,
            Exposure = AdjExposure,
            Highlights = AdjHighlights,
            Shadows = AdjShadows,
            Denoise = AdjDenoise,
            AutoEnhance = AdjAutoEnhance,
            Vibrance = AdjVibrance,
            Clarity = AdjClarity,
            Fade = AdjFade,
            Grain = AdjGrain,
            Monochrome = AdjMonochrome,
            Dehaze = AdjDehaze,
            Soften = AdjSoften,
            SepiaTone = AdjSepiaTone,
            InvertAmount = AdjInvertAmount,
            PosterizeLevels = AdjPosterizeLevels
        };

        if (adjustments.IsIdentity)
        {
            ResultBitmap = OriginalBitmap;
            IsDirty = false;
            return;
        }

        try
        {
            using var adjustedBgr = ImageProcessingHelper.ApplyAdjustments(_sourceImage.FullBgr, adjustments);
            ResultBitmap = adjustedBgr.ToBitmapSource(_workingAlpha);
            IsDirty = true;
        }
        catch (Exception ex)
        {
            _log.Error("Adjustment preview failed", ex);
        }
    }

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
        AdjExposure = 1.0;
        AdjHighlights = 0.0;
        AdjShadows = 0.0;
        AdjDenoise = 0.0;
        AdjAutoEnhance = false;
        AdjVibrance = 0.0;
        AdjClarity = 0.0;
        AdjFade = 0.0;
        AdjGrain = 0.0;
        AdjMonochrome = 0.0;
        AdjDehaze = 0.0;
        AdjSoften = 0.0;
        AdjSepiaTone = 0.0;
        AdjInvertAmount = 0.0;
        AdjPosterizeLevels = 0;
        UpdateLivePreview();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
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
            SharpenStrength = AdjSharpenStrength,
            Exposure = AdjExposure,
            Highlights = AdjHighlights,
            Shadows = AdjShadows,
            Denoise = AdjDenoise,
            AutoEnhance = AdjAutoEnhance,
            Vibrance = AdjVibrance,
            Clarity = AdjClarity,
            Fade = AdjFade,
            Grain = AdjGrain,
            Monochrome = AdjMonochrome,
            Dehaze = AdjDehaze,
            Soften = AdjSoften,
            SepiaTone = AdjSepiaTone,
            InvertAmount = AdjInvertAmount,
            PosterizeLevels = AdjPosterizeLevels
        };

        if (!adjustments.IsIdentity)
        {
            var adjustedBgr = ImageProcessingHelper.ApplyAdjustments(_sourceImage.FullBgr, adjustments);
            _parentDocument.ApplyToolResult(adjustedBgr, _workingAlpha.Clone(), "Adjustments");
        }

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }


    public override void Dispose()
    {
        _workingBgr?.Dispose();
        base.Dispose();
    }
}
