using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class NoiseToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "📡 Noise";
    public override string AccentColor => "#4B5563";

    [ObservableProperty]
    private double _noiseStrength = 20;

    [ObservableProperty]
    private bool _gaussianNoise = true;

    protected override string OperationName => "Noise";

    public NoiseToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Add Gaussian or salt-and-pepper noise, then apply.";
    }

    partial void OnNoiseStrengthChanged(double value) => RefreshResult();
    partial void OnGaussianNoiseChanged(bool value) => RefreshResult();

    protected override Mat ApplyEffect(Mat src)
    {
        return GaussianNoise
            ? NoiseService.AddGaussianNoise(src, NoiseStrength)
            : NoiseService.AddSaltPepperNoise(src, NoiseStrength);
    }

    protected override void OnResetToolDefaults()
    {
        NoiseStrength = 20;
        GaussianNoise = true;
    }
}
