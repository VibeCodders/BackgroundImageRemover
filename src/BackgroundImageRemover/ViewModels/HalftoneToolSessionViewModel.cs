using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for the halftone dot-matrix rendering of the image.</summary>
public partial class HalftoneToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "◍ Halftone";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private int _cellSize = 6;

    [ObservableProperty]
    private WpfColor _dotColor = WpfColor.FromRgb(20, 20, 20);

    [ObservableProperty]
    private bool _invert;

    protected override string OperationName => "Halftone";

    protected override bool IsEffectActive => true;

    public HalftoneToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Render the image as halftone dots whose size follows the local brightness.")
    {
        RefreshPreview();
    }

    partial void OnCellSizeChanged(int value) => RefreshPreview();
    partial void OnDotColorChanged(WpfColor value) => RefreshPreview();
    partial void OnInvertChanged(bool value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => HalftoneService.Apply(bgr, CellSize, DotColor.ToVec3b(), Invert);

    protected override void OnReset()
    {
        CellSize = 6;
        DotColor = WpfColor.FromRgb(20, 20, 20);
        Invert = false;
        RefreshPreview();
    }
}
