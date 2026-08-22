using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for selective and whole-image sharpening.</summary>
public partial class SharpenToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "🔪 Sharpen";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private double _strength = 0.5;

    protected override string OperationName => "Sharpen";

    public SharpenToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Choose whole-image or paint a region to sharpen, then apply.";
    }

    partial void OnStrengthChanged(double value) => RefreshResult();

    protected override void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;

        Mat result;
        if (WholeImage)
        {
            result = SharpenService.SharpenAll(_sourceImage!.FullBgr, Strength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            result = SharpenService.SharpenRegion(_sourceImage!.FullBgr, _paintedMask!, Strength);
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
            return SharpenService.SharpenAll(src, Strength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            return SharpenService.SharpenRegion(src, _paintedMask!, Strength);
        }
        return src.Clone();
    }

    [RelayCommand]
    private void Reset()
    {
        BrushRadius = 40;
        Strength = 0.5;
        WholeImage = false;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }
}
