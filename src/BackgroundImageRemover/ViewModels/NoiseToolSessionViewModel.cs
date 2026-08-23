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

    protected override void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;

        Mat result;
        if (WholeImage)
        {
            result = GaussianNoise
                ? NoiseService.AddGaussianNoise(_sourceImage!.FullBgr, NoiseStrength)
                : NoiseService.AddSaltPepperNoise(_sourceImage!.FullBgr, NoiseStrength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            result = GaussianNoise
                ? NoiseService.AddGaussianNoise(_sourceImage!.FullBgr, NoiseStrength)
                : NoiseService.AddSaltPepperNoise(_sourceImage!.FullBgr, NoiseStrength);
            result = result.BlendByMask(_sourceImage!.FullBgr, _paintedMask!);
        }
        else
        {
            result = _sourceImage!.FullBgr.Clone();
        }

        using var _ = result;
        ResultBitmap = result.ToBitmapSource(_workingAlpha!);
        IsDirty = IsEffectActive;
    }

    protected override Mat BuildResult(Mat src)
    {
        if (WholeImage)
        {
            return GaussianNoise
                ? NoiseService.AddGaussianNoise(src, NoiseStrength)
                : NoiseService.AddSaltPepperNoise(src, NoiseStrength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            var noisy = GaussianNoise
                ? NoiseService.AddGaussianNoise(src, NoiseStrength)
                : NoiseService.AddSaltPepperNoise(src, NoiseStrength);
            return noisy.BlendByMask(src, _paintedMask!);
        }
        return src.Clone();
    }

    protected override void OnReset()
    {
        NoiseStrength = 20;
        GaussianNoise = true;
        WholeImage = false;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }
}
