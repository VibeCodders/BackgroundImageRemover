using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for overlaying decorative blurred circles (bokeh) on the image.</summary>
public partial class BokehToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "● Bokeh";
    public override string AccentColor => "#06B6D4";

    [ObservableProperty]
    private WpfColor _color = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private int _radius = 14;

    [ObservableProperty]
    private int _count = 100;

    [ObservableProperty]
    private double _opacity = 0.9;

    [ObservableProperty]
    private int _blur = 6;

    protected override string OperationName => "Bokeh";

    protected override bool IsEffectActive => Count > 0 && Opacity > 1e-4;

    public BokehToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Overlay decorative blurred circles on the image.")
    {
        RefreshPreview();
    }

    partial void OnColorChanged(WpfColor value) => RefreshPreview();
    partial void OnRadiusChanged(int value) => RefreshPreview();
    partial void OnCountChanged(int value) => RefreshPreview();
    partial void OnOpacityChanged(double value) => RefreshPreview();
    partial void OnBlurChanged(int value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => BokehService.Apply(bgr, Color.ToVec3b(), Radius, Count, Opacity, Blur);

    protected override void OnResetDefaults()
    {
        Color = WpfColor.FromRgb(255, 255, 255);
        Radius = 14;
        Count = 100;
        Opacity = 0.9;
        Blur = 6;
    }
}
