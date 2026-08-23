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

    protected override void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;

        Mat result;
        if (WholeImage)
        {
            result = MotionBlur
                ? BlurService.MotionBlur(_sourceImage!.FullBgr, BlurRadius, MotionAngle)
                : BlurService.BlurAll(_sourceImage!.FullBgr, BlurRadius);
        }
        else if (PaintMode && HasPaintedMask)
        {
            result = BlurService.BlurRegion(_sourceImage!.FullBgr, _paintedMask!, BlurRadius);
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
            return MotionBlur
                ? BlurService.MotionBlur(src, BlurRadius, MotionAngle)
                : BlurService.BlurAll(src, BlurRadius);
        }
        else if (PaintMode && HasPaintedMask)
        {
            return BlurService.BlurRegion(src, _paintedMask!, BlurRadius);
        }
        return src.Clone();
    }

    protected override void OnReset()
    {
        BrushRadius = 40;
        BlurRadius = 12;
        WholeImage = false;
        MotionBlur = false;
        MotionAngle = 0;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }
}
