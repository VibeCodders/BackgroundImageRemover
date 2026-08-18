using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class RetouchToolSessionView : UserControl
{
    private RetouchToolSessionViewModel? ViewModel => DataContext as RetouchToolSessionViewModel;

    public RetouchToolSessionView()
    {
        InitializeComponent();
    }

    private void ResultEditPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnResultStrokeStart(e);
    private void ResultEditPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnResultStrokeMove(e);
    private void ResultEditPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnResultStrokeEnd();
    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnResultWandClicked(e);
}
