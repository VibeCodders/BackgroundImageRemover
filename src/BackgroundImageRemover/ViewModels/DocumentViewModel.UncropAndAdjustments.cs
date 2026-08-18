using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Outpaint;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;
    public IReadOnlyList<UncropInpaintMethod> InpaintMethods { get; } = Enum.GetValues<UncropInpaintMethod>();
    public IReadOnlyList<UncropMirrorType> MirrorTypes { get; } = Enum.GetValues<UncropMirrorType>();
    public IReadOnlyList<UncropColorSource> ColorSources { get; } = Enum.GetValues<UncropColorSource>();
    public IReadOnlyList<UncropGradientMode> GradientModes { get; } = Enum.GetValues<UncropGradientMode>();

    // --- Uncrop Options ---
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyUncropCommand))]
    private CanvasPadding _uncropPadding = CanvasPadding.Zero;

    [ObservableProperty]
    private UncropAspectPreset _selectedUncropPreset = UncropAspectPresets.Free;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyUncropCommand))]
    private UncropFillMode _selectedUncropFillMode = UncropFillMode.Mirror;

    [ObservableProperty]
    private UncropMirrorType _selectedUncropMirrorType = UncropMirrorType.Reflect101;

    [ObservableProperty]
    private int _uncropMirrorBlurRadius = 0;

    [ObservableProperty]
    private double _uncropMirrorFadeOpacity = 1.0;

    [ObservableProperty]
    private UncropInpaintMethod _selectedUncropInpaintMethod = UncropInpaintMethod.Telea;

    [ObservableProperty]
    private double _uncropInpaintRadius = 5.0;

    [ObservableProperty]
    private int _uncropBlendMargin = 0;

    [ObservableProperty]
    private bool _uncropInpaintPreFillEdgeAverage;

    [ObservableProperty]
    private UncropColorSource _selectedUncropColorSource = UncropColorSource.EdgeAverage;

    [ObservableProperty]
    private WpfColor _uncropCustomSolidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _isUncropColorPickerOpen;

    [ObservableProperty]
    private bool _uncropBlurredColorFill;

    [ObservableProperty]
    private int _uncropBlurRadius = 0;

    [ObservableProperty]
    private int _uncropReplicateSmoothRadius = 0;

    [ObservableProperty]
    private int _uncropZoomBlurRadius = 35;

    [ObservableProperty]
    private double _uncropZoomScale = 1.25;

    [ObservableProperty]
    private UncropGradientMode _selectedUncropGradientMode = UncropGradientMode.PerEdgeSplay;

    [ObservableProperty]
    private double _uncropGradientNoiseAmount = 0.0;

    [ObservableProperty]
    private int _uncropPatchSize = 32;

    [ObservableProperty]
    private int _uncropPatchBlendOverlap = 8;

    public int UncropPaddingLeftPx
    {
        get => UncropPadding.Left;
        set => SetUncropPaddingFromUser(UncropPadding with { Left = Math.Max(0, value) });
    }

    public int UncropPaddingTopPx
    {
        get => UncropPadding.Top;
        set => SetUncropPaddingFromUser(UncropPadding with { Top = Math.Max(0, value) });
    }

    public int UncropPaddingRightPx
    {
        get => UncropPadding.Right;
        set => SetUncropPaddingFromUser(UncropPadding with { Right = Math.Max(0, value) });
    }

    public int UncropPaddingBottomPx
    {
        get => UncropPadding.Bottom;
        set => SetUncropPaddingFromUser(UncropPadding with { Bottom = Math.Max(0, value) });
    }

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

    partial void OnUncropPaddingChanged(CanvasPadding value)
    {
        OnPropertyChanged(nameof(UncropPaddingLeftPx));
        OnPropertyChanged(nameof(UncropPaddingTopPx));
        OnPropertyChanged(nameof(UncropPaddingRightPx));
        OnPropertyChanged(nameof(UncropPaddingBottomPx));
    }

    partial void OnSelectedUncropPresetChanged(UncropAspectPreset value)
    {
        if (_loadedImage is null || value.Ratio is not { } ratio)
        {
            return;
        }
        UncropPadding = CanvasPadding.ComputeCentered(_loadedImage.FullBgr.Size(), ratio);
    }


    private void SetUncropPaddingFromUser(CanvasPadding value)
    {
        if (UncropPadding.Equals(value))
        {
            return;
        }
        UncropPadding = value;
        if (SelectedUncropPreset.Ratio is not null)
        {
            SelectedUncropPreset = UncropAspectPresets.Custom;
        }
    }


    // --- Uncrop Commands ---
    private bool CanApplyUncrop() => IsImageLoaded && !IsBusy
        && SelectedUncropFillMode != UncropFillMode.AiOutpaint
        && !UncropPadding.IsZero;

    private bool CanCancelUncrop() => IsBusy && _uncropCts is not null && !_uncropCts.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanCancelUncrop))]
    private void CancelUncrop()
    {
        if (_uncropCts is not null && !_uncropCts.IsCancellationRequested)
        {
            _uncropCts.Cancel();
            StatusMessage = "Cancelling uncrop operation...";
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyUncrop))]
    private async Task ApplyUncropAsync()
    {
        if (_loadedImage is null)
        {
            return;
        }

        var sourceBgr = _loadedImage.FullBgr;
        var padding = UncropPadding;
        var mode = SelectedUncropFillMode;
        var mirrorType = SelectedUncropMirrorType;
        var mirrorBlur = UncropMirrorBlurRadius;
        var mirrorFade = UncropMirrorFadeOpacity;
        var inpaintMethod = SelectedUncropInpaintMethod;
        var inpaintRadius = UncropInpaintRadius;
        var blendMargin = UncropBlendMargin;
        var inpaintPreFill = UncropInpaintPreFillEdgeAverage;
        var blurred = UncropBlurredColorFill;
        var blurRadius = UncropBlurRadius;
        var replicateSmooth = UncropReplicateSmoothRadius;
        var zoomBlurRadius = UncropZoomBlurRadius;
        var zoomScale = UncropZoomScale;
        var gradientMode = SelectedUncropGradientMode;
        var gradientNoise = UncropGradientNoiseAmount;
        var patchSize = UncropPatchSize;
        var patchOverlap = UncropPatchBlendOverlap;
        var colorSource = SelectedUncropColorSource;
        var customColor = colorSource == UncropColorSource.CustomColor
            ? new Scalar(UncropCustomSolidColor.B, UncropCustomSolidColor.G, UncropCustomSolidColor.R)
            : (Scalar?)null;

        _uncropCts?.Dispose();
        _uncropCts = new CancellationTokenSource();
        var ct = _uncropCts.Token;

        try
        {
            IsBusy = true;
            CancelUncropCommand.NotifyCanExecuteChanged();
            StatusMessage = "Applying uncrop expansion...";

            using var filledBgr = await Task.Run(() => mode switch
            {
                UncropFillMode.Mirror => _uncropFillService.FillMirror(sourceBgr, padding, mirrorType, mirrorBlur, mirrorFade, ct),
                UncropFillMode.Inpaint => _uncropFillService.FillInpaint(sourceBgr, padding, inpaintMethod, inpaintRadius, blendMargin, inpaintPreFill, ct),
                UncropFillMode.SolidColor => _uncropFillService.FillSolidColor(sourceBgr, padding, blurred, customColor, blurRadius, ct),
                UncropFillMode.Replicate => _uncropFillService.FillReplicate(sourceBgr, padding, replicateSmooth, ct),
                UncropFillMode.Wrap => _uncropFillService.FillWrap(sourceBgr, padding, ct),
                UncropFillMode.ZoomBlur => _uncropFillService.FillZoomBlur(sourceBgr, padding, zoomBlurRadius, zoomScale, blendMargin, ct),
                UncropFillMode.EdgeGradient => _uncropFillService.FillEdgeGradient(sourceBgr, padding, gradientMode, customColor, gradientNoise, ct),
                UncropFillMode.PatchSynthesis => _uncropFillService.FillPatchSynthesis(sourceBgr, padding, patchSize, patchOverlap, blendMargin, ct),
                _ => throw new InvalidOperationException($"Fill mode {mode} is not available.")
            }, ct);

            // Create new LoadedImage from the filled result
            var newLoadedImage = new LoadedImage(_loadedImage.FilePath, filledBgr.Clone());
            _loadedImage?.Dispose();
            _preview?.Dispose();
            _loadedImage = newLoadedImage;

            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;
            PreviewBitmap = preview.Bgr.ToBitmapSource();

            // Set as new working image
            DisposeWorkingResult();
            _workingBgr = _loadedImage.FullBgr.Clone();
            _workingAlpha = new Mat(_loadedImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
            _workingResultIsLoadedCutout = false;
            _workingResultHandEdited = true;

            _editHistory.Clear();
            RefreshUndoRedoState();
            RefreshResultBitmapFromWorking();

            UncropPadding = CanvasPadding.Zero;
            SelectedUncropPreset = UncropAspectPresets.Free;
            IsDirty = true;
            StatusMessage = $"Applied {mode} uncrop ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height}).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Uncrop operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Uncrop failed: {ex.Message}";
            _log.Error("Uncrop failed", ex);
        }
        finally
        {
            _uncropCts?.Dispose();
            _uncropCts = null;
            IsBusy = false;
            CancelUncropCommand.NotifyCanExecuteChanged();
        }
    }

    // --- Image Adjustments Execution ---

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

            var adjustedFullBgr = await Task.Run(() => ImageProcessingHelper.ApplyAdjustments(_loadedImage.FullBgr, adjustments));

            var newLoadedImage = new LoadedImage(_loadedImage.FilePath, adjustedFullBgr);
            _loadedImage.Dispose();
            _preview?.Dispose();
            _loadedImage = newLoadedImage;

            var preview = _downscaler.CreatePreview(_loadedImage.FullBgr);
            _preview = preview;
            PreviewBitmap = preview.Bgr.ToBitmapSource();

            // Also apply to working BGR if present
            if (_workingBgr is not null)
            {
                var adjustedWorking = ImageProcessingHelper.ApplyAdjustments(_workingBgr, adjustments);
                _workingBgr.Dispose();
                _workingBgr = adjustedWorking;
            }

            _workingResultHandEdited = true;
            IsDirty = true;
            RefreshResultBitmapFromWorking();

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
