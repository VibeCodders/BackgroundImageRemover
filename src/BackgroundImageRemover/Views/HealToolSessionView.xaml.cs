using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

namespace BackgroundImageRemover.Views;

public partial class HealToolSessionView : UserControl
{
    private HealToolSessionViewModel? ViewModel => DataContext as HealToolSessionViewModel;

    public HealToolSessionView()
    {
        InitializeComponent();
    }

    private void HealPreview_StrokeStart(object? sender, Point e)
        => ViewModel?.OnResultStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void HealPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnResultStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void HealPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
