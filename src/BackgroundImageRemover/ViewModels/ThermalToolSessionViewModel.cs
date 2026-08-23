using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for a thermal / heatmap palette mapped onto image luminance.</summary>
public partial class ThermalToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🌡 Thermal";
    public override string AccentColor => "#DC2626";

    [ObservableProperty]
    private double _intensity = 1.0;

    [ObservableProperty]
    private bool _invert;

    protected override string OperationName => "Thermal";

    protected override bool IsEffectActive => Intensity > 1e-4 || Invert;

    public ThermalToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Map image brightness onto a thermal (heatmap) palette.")
    {
        RefreshPreview();
    }

    partial void OnIntensityChanged(double value) => RefreshPreview();
    partial void OnInvertChanged(bool value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => ThermalService.Apply(bgr, Intensity, Invert);

    protected override void OnResetDefaults()
    {
        Intensity = 1.0;
        Invert = false;
    }
}
