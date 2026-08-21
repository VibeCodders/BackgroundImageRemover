using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class HealToolSessionView : UserControl
{
    private HealToolSessionViewModel? ViewModel => DataContext as HealToolSessionViewModel;

    public HealToolSessionView()
    {
        InitializeComponent();
    }

    private void HealPreview_StrokeStart(object? sender, Point e)
        => ViewModel?.OnResultStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void HealPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnResultStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void HealPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();
}
