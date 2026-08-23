using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for replacing a color (and similar hues) with another color.</summary>
public partial class ColorReplaceToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🎨 Color Replace";
    public override string AccentColor => "#EC4899";

    [ObservableProperty]
    private WpfColor _targetColor = WpfColor.FromRgb(255, 0, 0);

    [ObservableProperty]
    private WpfColor _replacementColor = WpfColor.FromRgb(0, 255, 0);

    [ObservableProperty]
    private double _tolerance = 0.25;

    [ObservableProperty]
    private double _softness = 0.4;

    [ObservableProperty]
    private bool _preserveLuminance = true;

    protected override string OperationName => "Color Replace";

    protected override bool IsEffectActive => Tolerance > 1e-4;

    public ColorReplaceToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Replace a target color (and close hues) with another color.")
    {
        RefreshPreview();
    }

    partial void OnTargetColorChanged(WpfColor value) => RefreshPreview();
    partial void OnReplacementColorChanged(WpfColor value) => RefreshPreview();
    partial void OnToleranceChanged(double value) => RefreshPreview();
    partial void OnSoftnessChanged(double value) => RefreshPreview();
    partial void OnPreserveLuminanceChanged(bool value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => ColorReplaceService.Apply(bgr, TargetColor.ToVec3b(), ReplacementColor.ToVec3b(),
            Tolerance, Softness, PreserveLuminance);

    /// <summary>Sets the target color from a click on the preview image.</summary>
    public void OnImageClicked(WpfPoint imagePoint)
    {
        if (_sourceImage is null)
        {
            return;
        }

        int x = (int)Math.Round(imagePoint.X);
        int y = (int)Math.Round(imagePoint.Y);
        Vec3b bgr = ColorPickerService.Sample(_sourceImage.FullBgr, x, y);
        TargetColor = WpfColor.FromRgb(bgr[2], bgr[1], bgr[0]);
        StatusMessage = $"Target color sampled at ({x}, {y}).";
    }

    protected override void OnResetDefaults()
    {
        TargetColor = WpfColor.FromRgb(255, 0, 0);
        ReplacementColor = WpfColor.FromRgb(0, 255, 0);
        Tolerance = 0.25;
        Softness = 0.4;
        PreserveLuminance = true;
    }
}
