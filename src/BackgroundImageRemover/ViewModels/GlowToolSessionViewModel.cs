using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for a glow / bloom effect around bright areas.</summary>
public partial class GlowToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "✨ Glow";
    public override string AccentColor => "#FFB300";

    [ObservableProperty]
    private int _threshold = 180;

    [ObservableProperty]
    private int _radius = 20;

    [ObservableProperty]
    private double _strength = 0.8;

    protected override string OperationName => "Glow";

    protected override bool IsEffectActive => Strength > 1e-4;

    public GlowToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Make the bright areas of the image radiate a soft glow.")
    {
        RefreshPreview();
    }

    partial void OnThresholdChanged(int value) => RefreshPreview();
    partial void OnRadiusChanged(int value) => RefreshPreview();
    partial void OnStrengthChanged(double value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => GlowService.Apply(bgr, Threshold, Radius, Strength);

    protected override void OnReset()
    {
        Threshold = 180;
        Radius = 20;
        Strength = 0.8;
        RefreshPreview();
    }
}
