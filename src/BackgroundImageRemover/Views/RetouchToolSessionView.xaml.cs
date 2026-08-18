using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

namespace BackgroundImageRemover.Views;

public partial class RetouchToolSessionView : UserControl
{
    private RetouchToolSessionViewModel? ViewModel => DataContext as RetouchToolSessionViewModel;

    public RetouchToolSessionView()
    {
        InitializeComponent();
    }

    private void ResultEditPreview_StrokeStart(object? sender, Point e)
    {
        if (ViewModel is null) return;
        ViewModel.OnResultStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));
    }

    private void ResultEditPreview_StrokeMove(object? sender, Point e)
    {
        if (ViewModel is null) return;
        ViewModel.OnResultStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));
    }

    private void ResultEditPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();
    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnResultWandClicked(e);

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
