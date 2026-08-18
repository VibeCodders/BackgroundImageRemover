using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Reusable uncrop (canvas outpainting) options: the fill-mode parameters, the padding/preset
/// state, and the shared aspect-ratio + hand-edit padding logic. Composed by
/// <see cref="DocumentViewModel"/>, <see cref="UncropViewModel"/> and
/// <see cref="UncropToolSessionViewModel"/> so the three surfaces stay in sync without
/// duplicating ~20 observable properties each.
/// </summary>
public partial class UncropOptionsViewModel : ObservableObject
{
    public IReadOnlyList<UncropAspectPreset> AspectPresets { get; } = UncropAspectPresets.All;
    public IReadOnlyList<UncropSizePreset> SizePresets { get; } = UncropSizePresets.All;
    public IReadOnlyList<UncropInpaintMethod> InpaintMethods { get; } = Enum.GetValues<UncropInpaintMethod>();
    public IReadOnlyList<UncropMirrorType> MirrorTypes { get; } = Enum.GetValues<UncropMirrorType>();
    public IReadOnlyList<UncropColorSource> ColorSources { get; } = Enum.GetValues<UncropColorSource>();
    public IReadOnlyList<UncropGradientMode> GradientModes { get; } = Enum.GetValues<UncropGradientMode>();

    [ObservableProperty]
    private CanvasPadding _padding = CanvasPadding.Zero;

    [ObservableProperty]
    private UncropAspectPreset _selectedPreset = UncropAspectPresets.Free;

    [ObservableProperty]
    private UncropSizePreset _selectedSizePreset = UncropSizePresets.None;

    [ObservableProperty]
    private UncropFillMode _selectedFillMode = UncropFillMode.Mirror;

    [ObservableProperty]
    private UncropMirrorType _selectedMirrorType = UncropMirrorType.Reflect101;

    [ObservableProperty]
    private int _mirrorBlurRadius = 0;

    [ObservableProperty]
    private double _mirrorFadeOpacity = 1.0;

    [ObservableProperty]
    private UncropInpaintMethod _selectedInpaintMethod = UncropInpaintMethod.Telea;

    [ObservableProperty]
    private double _inpaintRadius = 5.0;

    [ObservableProperty]
    private int _blendMargin = 0;

    [ObservableProperty]
    private bool _inpaintPreFillEdgeAverage;

    [ObservableProperty]
    private UncropColorSource _selectedColorSource = UncropColorSource.EdgeAverage;

    [ObservableProperty]
    private WpfColor _customSolidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _blurredColorFill;

    [ObservableProperty]
    private int _blurRadius = 0;

    [ObservableProperty]
    private int _replicateSmoothRadius = 0;

    [ObservableProperty]
    private int _zoomBlurRadius = 35;

    [ObservableProperty]
    private double _zoomScale = 1.25;

    [ObservableProperty]
    private UncropGradientMode _selectedGradientMode = UncropGradientMode.PerEdgeSplay;

    [ObservableProperty]
    private double _gradientNoiseAmount = 0.0;

    [ObservableProperty]
    private int _patchSize = 32;

    [ObservableProperty]
    private int _patchBlendOverlap = 8;

    // Post-fill finishing options (applied to the whole result).
    [ObservableProperty]
    private int _cornerRadius = 0;

    [ObservableProperty]
    private int _borderThickness = 0;

    [ObservableProperty]
    private WpfColor _borderColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _isBorderColorPickerOpen;

    [ObservableProperty]
    private double _grainAmount = 0.0;

    [ObservableProperty]
    private bool _flipHorizontal;

    [ObservableProperty]
    private bool _flipVertical;

    [ObservableProperty]
    private double _rotateAngle;

    [ObservableProperty]
    private double _vignette;

    [ObservableProperty]
    private double _saturation = 1.0;

    [ObservableProperty]
    private double _contrast = 1.0;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private double _sharpenStrength;

    [ObservableProperty]
    private int _finishBlurRadius;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private double _tint;

    [ObservableProperty]
    private double _denoise;

    /// <summary>
    /// Supplies the current source-image size so an aspect-ratio preset can compute a centered
    /// padding. Set by the hosting ViewModel; null until an image is available.
    /// </summary>
    public Func<Size?>? ImageSizeProvider { get; set; }

    public int PaddingLeftPx
    {
        get => Padding.Left;
        set => SetPaddingFromUser(Padding with { Left = Math.Max(0, value) });
    }

    public int PaddingTopPx
    {
        get => Padding.Top;
        set => SetPaddingFromUser(Padding with { Top = Math.Max(0, value) });
    }

    public int PaddingRightPx
    {
        get => Padding.Right;
        set => SetPaddingFromUser(Padding with { Right = Math.Max(0, value) });
    }

    public int PaddingBottomPx
    {
        get => Padding.Bottom;
        set => SetPaddingFromUser(Padding with { Bottom = Math.Max(0, value) });
    }

    partial void OnPaddingChanged(CanvasPadding value)
    {
        OnPropertyChanged(nameof(PaddingLeftPx));
        OnPropertyChanged(nameof(PaddingTopPx));
        OnPropertyChanged(nameof(PaddingRightPx));
        OnPropertyChanged(nameof(PaddingBottomPx));
    }

    partial void OnSelectedPresetChanged(UncropAspectPreset value)
    {
        if (value.Ratio is not { } ratio)
        {
            return;
        }
        if (ImageSizeProvider?.Invoke() is not { } imageSize)
        {
            return;
        }
        Padding = CanvasPadding.ComputeCentered(imageSize, ratio);
        if (value.Ratio is not null)
        {
            SelectedSizePreset = UncropSizePresets.None;
        }
    }

    partial void OnSelectedSizePresetChanged(UncropSizePreset value)
    {
        if (value.Size is not { } target)
        {
            return;
        }
        if (ImageSizeProvider?.Invoke() is not { } imageSize)
        {
            return;
        }

        int extraW = Math.Max(0, target.Width - imageSize.Width);
        int extraH = Math.Max(0, target.Height - imageSize.Height);
        int left = extraW / 2;
        int top = extraH / 2;
        Padding = new CanvasPadding(left, top, extraW - left, extraH - top);
        SelectedPreset = UncropAspectPresets.Custom;
    }

    /// <summary>Applies a padding change coming from the handles or the numeric fields: if a
    /// specific ratio preset was active, hand-editing drops it to "Custom" so the preset buttons
    /// stop fighting the manual change.</summary>
    private void SetPaddingFromUser(CanvasPadding value)
    {
        if (Padding.Equals(value))
        {
            return;
        }
        Padding = value;
        if (SelectedPreset.Ratio is not null)
        {
            SelectedPreset = UncropAspectPresets.Custom;
        }
        if (SelectedSizePreset.Size is not null)
        {
            SelectedSizePreset = UncropSizePresets.None;
        }
    }

    /// <summary>Resets padding and preset to the default free-form state.</summary>
    public void Reset()
    {
        Padding = CanvasPadding.Zero;
        SelectedPreset = UncropAspectPresets.Free;
        SelectedSizePreset = UncropSizePresets.None;
        CornerRadius = 0;
        BorderThickness = 0;
        GrainAmount = 0.0;
        FlipHorizontal = false;
        FlipVertical = false;
        RotateAngle = 0.0;
        Vignette = 0.0;
        Saturation = 1.0;
        Contrast = 1.0;
        Brightness = 0.0;
        SharpenStrength = 0.0;
        FinishBlurRadius = 0;
        Temperature = 0.0;
        Tint = 0.0;
        Denoise = 0.0;
    }

    /// <summary>Builds the operation config consumed by <see cref="UncropOperationHelper"/>.</summary>
    public UncropOperationHelper.UncropConfig ToConfig() => new()
    {
        Padding = Padding,
        FillMode = SelectedFillMode,
        MirrorType = SelectedMirrorType,
        MirrorBlurRadius = MirrorBlurRadius,
        MirrorFadeOpacity = MirrorFadeOpacity,
        InpaintMethod = SelectedInpaintMethod,
        InpaintRadius = InpaintRadius,
        BlendMargin = BlendMargin,
        InpaintPreFillEdgeAverage = InpaintPreFillEdgeAverage,
        BlurredColorFill = BlurredColorFill,
        BlurRadius = BlurRadius,
        ReplicateSmoothRadius = ReplicateSmoothRadius,
        ZoomBlurRadius = ZoomBlurRadius,
        ZoomScale = ZoomScale,
        GradientMode = SelectedGradientMode,
        GradientNoiseAmount = GradientNoiseAmount,
        PatchSize = PatchSize,
        PatchBlendOverlap = PatchBlendOverlap,
        ColorSource = SelectedColorSource,
        CustomSolidColor = CustomSolidColor,
        CornerRadius = CornerRadius,
        BorderThickness = BorderThickness,
        BorderColor = BorderColor,
        GrainAmount = GrainAmount,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
        RotateAngle = RotateAngle,
        Vignette = Vignette,
        Saturation = Saturation,
        Contrast = Contrast,
        Brightness = Brightness,
        SharpenStrength = SharpenStrength,
        FinishBlurRadius = FinishBlurRadius,
        Temperature = Temperature,
        Tint = Tint,
        Denoise = Denoise
    };

    /// <summary>True when the current options describe a runnable uncrop operation.</summary>
    public bool CanExecute() => UncropOperationHelper.CanExecute(ToConfig());
}
