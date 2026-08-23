using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for the cartoon effect (smooth colors + dark outlines).</summary>
public partial class CartoonToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🎭 Cartoon";
    public override string AccentColor => "#A855F7";

    [ObservableProperty]
    [ToolParameter]
    private int _smoothness = 8;

    [ObservableProperty]
    [ToolParameter]
    private int _quantizeLevels = 8;

    [ObservableProperty]
    [ToolParameter]
    private int _edgeThreshold = 12;

    protected override string OperationName => "Cartoon";

    protected override bool IsEffectActive => true;

    public CartoonToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Smooth the image into flat colors with dark outlines.")
    {
        RefreshPreview();
    }

    protected override Mat ApplyEffect(Mat bgr)
        => CartoonService.Apply(bgr, Smoothness, QuantizeLevels, EdgeThreshold);

    protected override void OnResetDefaults()
    {
        Smoothness = 8;
        QuantizeLevels = 8;
        EdgeThreshold = 12;
    }
}
