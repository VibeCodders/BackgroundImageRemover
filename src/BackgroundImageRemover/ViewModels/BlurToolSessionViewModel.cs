using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for selective and whole-image blur.</summary>
public partial class BlurToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "🌫 Blur";
    public override string AccentColor => "#0E7490";

    [ObservableProperty]
    private double _blurRadius = 12;

    [ObservableProperty]
    private bool _motionBlur;

    [ObservableProperty]
    private double _motionAngle;

    protected override string OperationName => "Blur";

    public BlurToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Choose whole-image or paint a region to blur, then apply.";
    }

    partial void OnBlurRadiusChanged(double value) => RefreshResult();
    partial void OnMotionBlurChanged(bool value) => RefreshResult();
    partial void OnMotionAngleChanged(double value) => RefreshResult();

    protected override Mat ApplyEffect(Mat src)
    {
        return MotionBlur
            ? BlurService.MotionBlur(src, BlurRadius, MotionAngle)
            : BlurService.BlurAll(src, BlurRadius);
    }

    protected override Mat ApplyEffectToRegion(Mat src, Mat mask)
        => BlurService.BlurRegion(src, mask, BlurRadius);

    protected override void OnResetToolDefaults()
    {
        BlurRadius = 12;
        MotionBlur = false;
        MotionAngle = 0;
    }
}
