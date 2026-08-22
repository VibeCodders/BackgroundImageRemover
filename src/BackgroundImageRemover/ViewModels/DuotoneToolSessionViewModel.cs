using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for mapping image luminance onto a two-color (duotone) palette.</summary>
public partial class DuotoneToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "◐ Duotone";
    public override string AccentColor => "#8B5CF6";

    [ObservableProperty]
    private WpfColor _darkColor = WpfColor.FromRgb(20, 20, 80);

    [ObservableProperty]
    private WpfColor _lightColor = WpfColor.FromRgb(255, 200, 40);

    [ObservableProperty]
    private double _midpoint = 0.5;

    [ObservableProperty]
    private double _strength = 1.0;

    [ObservableProperty]
    private bool _isDarkColorPickerOpen;

    [ObservableProperty]
    private bool _isLightColorPickerOpen;

    protected override string OperationName => "Duotone";

    protected override bool IsEffectActive => Strength > 1e-4;

    public DuotoneToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Map image brightness onto a two-color palette.")
    {
        RefreshPreview();
    }

    partial void OnDarkColorChanged(WpfColor value) => RefreshPreview();
    partial void OnLightColorChanged(WpfColor value) => RefreshPreview();
    partial void OnMidpointChanged(double value) => RefreshPreview();
    partial void OnStrengthChanged(double value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => DuotoneService.Apply(bgr, DarkColor.ToVec3b(), LightColor.ToVec3b(), Midpoint, Strength);

    [RelayCommand]
    private void Reset()
    {
        DarkColor = WpfColor.FromRgb(20, 20, 80);
        LightColor = WpfColor.FromRgb(255, 200, 40);
        Midpoint = 0.5;
        Strength = 1.0;
        RefreshPreview();
    }
}
