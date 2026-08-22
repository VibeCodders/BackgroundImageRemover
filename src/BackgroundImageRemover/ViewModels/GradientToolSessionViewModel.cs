using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for overlaying linear or radial color gradients.</summary>
public partial class GradientToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "◮ Gradient";
    public override string AccentColor => "#F59E0B";

    [ObservableProperty]
    private GradientKind _kind = GradientKind.Linear;

    [ObservableProperty]
    private WpfColor _colorA = WpfColor.FromRgb(255, 0, 0);

    [ObservableProperty]
    private WpfColor _colorB = WpfColor.FromRgb(0, 0, 255);

    [ObservableProperty]
    private double _angle = 90;

    [ObservableProperty]
    private double _opacity = 0.6;

    [ObservableProperty]
    private bool _isColorAPickerOpen;

    [ObservableProperty]
    private bool _isColorBPickerOpen;

    protected override string OperationName => "Gradient";

    protected override bool IsEffectActive => Opacity > 1e-4;

    public bool IsLinear => Kind == GradientKind.Linear;
    public double LinearVisibility => IsLinear ? 1 : 0;

    public Array GradientKinds => Enum.GetValues(typeof(GradientKind));

    public GradientToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Overlay a linear or radial gradient onto the image.")
    {
        RefreshPreview();
    }

    partial void OnKindChanged(GradientKind value)
    {
        OnPropertyChanged(nameof(IsLinear));
        OnPropertyChanged(nameof(LinearVisibility));
        RefreshPreview();
    }

    partial void OnColorAChanged(WpfColor value) => RefreshPreview();
    partial void OnColorBChanged(WpfColor value) => RefreshPreview();
    partial void OnAngleChanged(double value) => RefreshPreview();
    partial void OnOpacityChanged(double value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => GradientService.Apply(bgr, Kind, ColorA.ToVec3b(), ColorB.ToVec3b(), Angle, Opacity);

    [RelayCommand]
    private void Reset()
    {
        Kind = GradientKind.Linear;
        ColorA = WpfColor.FromRgb(255, 0, 0);
        ColorB = WpfColor.FromRgb(0, 0, 255);
        Angle = 90;
        Opacity = 0.6;
        RefreshPreview();
    }
}
