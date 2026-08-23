using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>A named pair of dark/light colors shown as a quick-applied preset in the Duotone tool.</summary>
public sealed record DuotonePreset(string Name, WpfColor Dark, WpfColor Light);

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

    /// <summary>Ready-made dark/light color pairs for one-click application.</summary>
    public IReadOnlyList<DuotonePreset> Presets { get; } =
    [
        new("Mono", WpfColor.FromRgb(0, 0, 0), WpfColor.FromRgb(255, 255, 255)),
        new("Black & Gold", WpfColor.FromRgb(0, 0, 0), WpfColor.FromRgb(255, 215, 0)),
        new("Navy & Amber", WpfColor.FromRgb(26, 26, 78), WpfColor.FromRgb(255, 195, 0)),
        new("Violet & Cyan", WpfColor.FromRgb(43, 10, 78), WpfColor.FromRgb(0, 229, 255)),
        new("Crimson & Rose", WpfColor.FromRgb(74, 0, 21), WpfColor.FromRgb(255, 107, 157)),
        new("Forest & Lime", WpfColor.FromRgb(10, 61, 26), WpfColor.FromRgb(180, 255, 57)),
        new("Sunset", WpfColor.FromRgb(74, 14, 14), WpfColor.FromRgb(255, 183, 77)),
        new("Ocean", WpfColor.FromRgb(0, 39, 74), WpfColor.FromRgb(144, 224, 239))
    ];

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
    private void ApplyPreset(DuotonePreset preset)
    {
        DarkColor = preset.Dark;
        LightColor = preset.Light;
    }

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
