using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for the tilt-shift / miniature effect.</summary>
public partial class TiltShiftToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "📐 Tilt-Shift";
    public override string AccentColor => "#4F46E5";

    [ObservableProperty]
    private double _focusCenter = 0.5;

    [ObservableProperty]
    private double _focusWidth = 0.35;

    [ObservableProperty]
    private double _blurRadius = 12;

    [ObservableProperty]
    private bool _vertical;

    [ObservableProperty]
    private double _saturationBoost = 0.3;

    protected override string OperationName => "Tilt-Shift";

    protected override bool IsEffectActive => BlurRadius > 0 || Math.Abs(SaturationBoost) > 1e-4;

    public TiltShiftToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Adjust the focus band, blur and saturation.")
    {
        RefreshPreview();
    }

    partial void OnFocusCenterChanged(double value) => RequestRefresh();
    partial void OnFocusWidthChanged(double value) => RequestRefresh();
    partial void OnBlurRadiusChanged(double value) => RequestRefresh();
    partial void OnVerticalChanged(bool value) => RequestRefresh();
    partial void OnSaturationBoostChanged(double value) => RequestRefresh();

    protected override Mat ApplyEffect(Mat bgr)
        => TiltShiftService.Apply(bgr, FocusCenter, FocusWidth, BlurRadius, Vertical, SaturationBoost);

    protected override void OnResetDefaults()
    {
        FocusCenter = 0.5;
        FocusWidth = 0.35;
        BlurRadius = 12;
        Vertical = false;
        SaturationBoost = 0.3;
    }
}
