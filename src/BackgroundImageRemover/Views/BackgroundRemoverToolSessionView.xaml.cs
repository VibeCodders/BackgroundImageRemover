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
        Loaded += BackgroundRemoverToolSessionView_Loaded;
        Unloaded += BackgroundRemoverToolSessionView_Unloaded;
    }

    private void BackgroundRemoverToolSessionView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.ScribbleStrokeUndone += ViewModel_ScribbleStrokeUndone;
        ViewModel.ScribbleStrokeRedone += ViewModel_ScribbleStrokeRedone;
        ViewModel.ScribblesCleared += ViewModel_ScribblesCleared;
    }

    private void BackgroundRemoverToolSessionView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.ScribbleStrokeUndone -= ViewModel_ScribbleStrokeUndone;
        ViewModel.ScribbleStrokeRedone -= ViewModel_ScribbleStrokeRedone;
        ViewModel.ScribblesCleared -= ViewModel_ScribblesCleared;
    }

    private void ViewModel_ScribbleStrokeUndone(object? sender, EventArgs e) => OriginalPreview.UndoScribbleStroke();
    private void ViewModel_ScribbleStrokeRedone(object? sender, EventArgs e) => OriginalPreview.RedoScribbleStroke();
    private void ViewModel_ScribblesCleared(object? sender, EventArgs e) => OriginalPreview.ClearScribbleStrokes();

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e)
    {
        if (ViewModel is not null) ViewModel.GrabCut.SelectedRect = e;
    }

    private void OriginalPreview_StrokeStart(object? sender, Point e) => ViewModel?.OnOriginalStrokeStart(e);
    private void OriginalPreview_StrokeMove(object? sender, Point e) => ViewModel?.OnOriginalStrokeMove(e);
    private void OriginalPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnOriginalStrokeEnd();
    private void OriginalPreview_SamPointClicked(object? sender, OpenCvSharp.Point e) => ViewModel?.OnOriginalSamPointClicked(e);
}
