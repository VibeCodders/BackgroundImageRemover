using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class BackgroundRemoverToolSessionView : UserControl
{
    private BackgroundRemoverToolSessionViewModel? ViewModel => DataContext as BackgroundRemoverToolSessionViewModel;

    public BackgroundRemoverToolSessionView()
    {
        InitializeComponent();
    }

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e)
    {
        if (ViewModel is not null) ViewModel.GrabCut.SelectedRect = e;
    }

    private void OriginalPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnOriginalStrokeStart(e);
    private void OriginalPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnOriginalStrokeMove(e);
    private void OriginalPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnOriginalStrokeEnd();
    private void OriginalPreview_SamPointClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalSamPointClicked(e);
    private void OriginalPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalWandClicked(e);
}
