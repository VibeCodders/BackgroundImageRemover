using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class HueSatToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "🎨 Hue / Sat";
    public override string AccentColor => "#BE185D";

    [ObservableProperty]
    private double _hueShift;

    [ObservableProperty]
    private double _saturation = 1;

    [ObservableProperty]
    private double _value = 1;

    protected override string OperationName => "HueSat";

    public HueSatToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Adjust hue, saturation and value, then apply.";
    }

    partial void OnHueShiftChanged(double value) => RefreshResult();
    partial void OnSaturationChanged(double value) => RefreshResult();
    partial void OnValueChanged(double value) => RefreshResult();

    protected override void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;

        Mat result;
        if (WholeImage)
        {
            result = HueSatService.AdjustHueSat(_sourceImage!.FullBgr, HueShift, Saturation, Value);
        }
        else if (PaintMode && HasPaintedMask)
        {
            result = HueSatService.AdjustHueSatRegion(_sourceImage!.FullBgr, _paintedMask!, HueShift, Saturation, Value);
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
            return HueSatService.AdjustHueSat(src, HueShift, Saturation, Value);
        }
        else if (PaintMode && HasPaintedMask)
        {
            return HueSatService.AdjustHueSatRegion(src, _paintedMask!, HueShift, Saturation, Value);
        }
        return src.Clone();
    }

    [RelayCommand]
    private void Reset()
    {
        HueShift = 0;
        Saturation = 1;
        Value = 1;
        WholeImage = false;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        RefreshResult();
    }
}
