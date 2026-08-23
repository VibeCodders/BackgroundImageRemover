using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for an emboss (relief) filter with a selectable light direction.</summary>
public partial class EmbossToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🗻 Emboss";
    public override string AccentColor => "#92400E";

    [ObservableProperty]
    private double _angle = 135;

    [ObservableProperty]
    private double _strength = 1.0;

    [ObservableProperty]
    private bool _grayscale = true;

    protected override string OperationName => "Emboss";

    protected override bool IsEffectActive => true;

    public EmbossToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Emboss the image; the angle sets the light direction.")
    {
        RefreshPreview();
    }

    partial void OnAngleChanged(double value) => RefreshPreview();
    partial void OnStrengthChanged(double value) => RefreshPreview();
    partial void OnGrayscaleChanged(bool value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => EmbossService.Apply(bgr, Angle, Strength, Grayscale);

    [RelayCommand]
    private void Reset()
    {
        Angle = 135;
        Strength = 1.0;
        Grayscale = true;
        RefreshPreview();
    }
}
