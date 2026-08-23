using BackgroundImageRemover.Helpers;
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
    [ToolParameter]
    private double _focusCenter = 0.5;

    [ObservableProperty]
    [ToolParameter]
    private double _focusWidth = 0.35;

    [ObservableProperty]
    [ToolParameter]
    private double _blurRadius = 12;

    [ObservableProperty]
    [ToolParameter]
    private bool _vertical;

    [ObservableProperty]
    [ToolParameter]
    private double _saturationBoost = 0.3;

    protected override string OperationName => "Tilt-Shift";

    protected override bool IsEffectActive => BlurRadius > 0 || Math.Abs(SaturationBoost) > 1e-4;

    public TiltShiftToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Adjust the focus band, blur and saturation.")
    {
        RefreshPreview();
    }

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
