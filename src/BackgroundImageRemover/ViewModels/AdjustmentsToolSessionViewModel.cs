using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for Color Filters, Brightness, Contrast, Saturation, Hue, Blur and Sharpen adjustments.
/// </summary>
public partial class AdjustmentsToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IFileLogService _log;
    private LoadedImage? _sourceImage;

    private Mat? _workingBgr;
    private Mat? _workingAlpha;

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
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingBgr = _sourceImage.FullBgr.Clone();
        _workingAlpha = _sourceImage.FullAlpha?.Clone() ?? new Mat(_workingBgr.Size(), MatType.CV_8UC1, new Scalar(255));

        using var bgra = new Mat();
        Cv2.CvtColor(_workingBgr, bgra, ColorConversionCodes.BGR2BGRA);
        BackgroundCompositingService.ReplaceAlphaChannel(bgra, _workingAlpha);
        OriginalBitmap = bgra.ToBitmapSource();
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
            SharpenStrength = AdjSharpenStrength
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
            using var bgra = new Mat();
            Cv2.CvtColor(adjustedBgr, bgra, ColorConversionCodes.BGR2BGRA);
            BackgroundCompositingService.ReplaceAlphaChannel(bgra, _workingAlpha);
            ResultBitmap = bgra.ToBitmapSource();
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
            SharpenStrength = AdjSharpenStrength
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
        _sourceImage?.Dispose();
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
    }
}
