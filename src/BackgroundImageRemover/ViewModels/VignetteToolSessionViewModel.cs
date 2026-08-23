using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for adding vignette (darken/lighten edges) effects.</summary>
public partial class VignetteToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🔳 Vignette";
    public override string AccentColor => "#A78BDA";

    [ObservableProperty]
    [ToolParameter]
    private double _strength = 0.3;

    [ObservableProperty]
    [ToolParameter]
    private double _roundness = 0.5;

    [ObservableProperty]
    [ToolParameter]
    private double _feather = 0.5;

    [ObservableProperty]
    [ToolParameter]
    private bool _invert;

    protected override string OperationName => "Vignette";

    protected override bool IsEffectActive => Strength > 1e-4;

    public VignetteToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Adjust vignette strength, roundness and feather.")
    {
        RefreshPreview();
    }

    protected override Mat ApplyEffect(Mat bgr)
        => VignetteService.Apply(bgr, Strength, Roundness, Feather, Invert);

    protected override void OnResetDefaults()
    {
        Strength = 0.3;
        Roundness = 0.5;
        Feather = 0.5;
        Invert = false;
    }
}
