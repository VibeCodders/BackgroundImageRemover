using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for the oil-painting effect (flat dominant colours).</summary>
public partial class OilPaintToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🖌 Oil Paint";
    public override string AccentColor => "#B45309";

    [ObservableProperty]
    private int _brushSize = 3;

    [ObservableProperty]
    private int _detail = 8;

    protected override string OperationName => "Oil Paint";

    protected override bool IsEffectActive => true;

    public OilPaintToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Oil-painting look: flat dominant colours in brush-sized neighbourhoods.")
    {
        RefreshPreview();
    }

    partial void OnBrushSizeChanged(int value) => RefreshPreview();
    partial void OnDetailChanged(int value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => OilPaintService.Apply(bgr, BrushSize, Detail);

    protected override void OnReset()
    {
        BrushSize = 3;
        Detail = 8;
        RefreshPreview();
    }
}
