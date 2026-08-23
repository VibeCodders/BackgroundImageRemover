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

    protected override Mat ApplyEffect(Mat src) => HueSatService.AdjustHueSat(src, HueShift, Saturation, Value);

    protected override Mat ApplyEffectToRegion(Mat src, Mat mask)
        => HueSatService.AdjustHueSatRegion(src, mask, HueShift, Saturation, Value);

    protected override void OnResetToolDefaults()
    {
        HueShift = 0;
        Saturation = 1;
        Value = 1;
    }
}
