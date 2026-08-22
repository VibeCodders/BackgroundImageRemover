using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class LassoSelectToolSessionView : UserControl
{
    private LassoSelectToolSessionViewModel? ViewModel => DataContext as LassoSelectToolSessionViewModel;

    public LassoSelectToolSessionView()
    {
        InitializeComponent();
    }

    private void LassoPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnStrokeStart(e);
    private void LassoPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnStrokeMove(e);
    private void LassoPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnStrokeEnd();
}
