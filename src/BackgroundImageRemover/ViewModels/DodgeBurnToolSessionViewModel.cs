using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class DodgeBurnToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "☀ Dodge / Burn";
    public override string AccentColor => "#B45309";

    [ObservableProperty]
    private bool _dodge = true;

    [ObservableProperty]
    private double _strength = 0.3;

    protected override string OperationName => "DodgeBurn";

    public DodgeBurnToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Dodge (lighten) or Burn (darken) a region, then apply.";
    }

    partial void OnDodgeChanged(bool value) => RefreshResult();
    partial void OnStrengthChanged(double value) => RefreshResult();

    protected override void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;

        Mat result;
        if (WholeImage)
        {
            result = DodgeBurnService.DodgeBurnAll(_sourceImage!.FullBgr, Dodge, Strength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            result = DodgeBurnService.DodgeBurnRegion(_sourceImage!.FullBgr, _paintedMask!, Dodge, Strength);
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
            return DodgeBurnService.DodgeBurnAll(src, Dodge, Strength);
        }
        else if (PaintMode && HasPaintedMask)
        {
            return DodgeBurnService.DodgeBurnRegion(src, _paintedMask!, Dodge, Strength);
        }
        return src.Clone();
    }

    [RelayCommand]
    private void Reset()
    {
        Dodge = true;
        Strength = 0.3;
        WholeImage = false;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }
}
