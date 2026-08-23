using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class RedEyeToolSessionView : UserControl
{
    private RedEyeToolSessionViewModel? ViewModel => DataContext as RedEyeToolSessionViewModel;

    public RedEyeToolSessionView()
    {
        InitializeComponent();
    }

    private void RedEyePreview_StrokeStart(object? sender, Point e) => ViewModel?.OnClick(e);
}
