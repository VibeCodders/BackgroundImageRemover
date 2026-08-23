using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for a sinusoidal (wave / ripple) distortion of the image.</summary>
public partial class WaveToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "〰 Wave";
    public override string AccentColor => "#0284C7";

    [ObservableProperty]
    private double _amplitude = 12;

    [ObservableProperty]
    private double _wavelength = 80;

    [ObservableProperty]
    private double _angle;

    protected override string OperationName => "Wave";

    protected override bool IsEffectActive => Amplitude > 0.5;

    public WaveToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Distort the image with waves; angle sets the ridge direction.")
    {
        RefreshPreview();
    }

    partial void OnAmplitudeChanged(double value) => RefreshPreview();
    partial void OnWavelengthChanged(double value) => RefreshPreview();
    partial void OnAngleChanged(double value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => WaveService.Apply(bgr, Amplitude, Wavelength, Angle);

    protected override void OnResetDefaults()
    {
        Amplitude = 12;
        Wavelength = 80;
        Angle = 0;
    }
}
