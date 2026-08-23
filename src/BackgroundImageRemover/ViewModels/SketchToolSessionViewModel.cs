using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for converting the image into a pencil sketch.</summary>
public partial class SketchToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "✏️ Sketch";
    public override string AccentColor => "#6B7280";

    [ObservableProperty]
    [ToolParameter]
    private int _blurRadius = 7;

    [ObservableProperty]
    [ToolParameter]
    private bool _invert;

    protected override string OperationName => "Sketch";

    protected override bool IsEffectActive => true;

    public SketchToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Convert the image to a pencil sketch.")
    {
        RefreshPreview();
    }

    protected override Mat ApplyEffect(Mat bgr)
        => SketchService.Apply(bgr, BlurRadius, Invert);

    protected override void OnResetDefaults()
    {
        BlurRadius = 7;
        Invert = false;
    }
}
