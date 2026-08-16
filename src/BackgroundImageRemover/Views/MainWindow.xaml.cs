using System.ComponentModel;
using System.Windows;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ScribbleStrokeUndone += (_, _) => OriginalPreview.UndoScribbleStroke();
        viewModel.ScribbleStrokeRedone += (_, _) => OriginalPreview.RedoScribbleStroke();
        viewModel.ScribblesCleared += (_, _) => OriginalPreview.ClearScribbleStrokes();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PreviewBitmap))
        {
            OriginalPreview.ResetView();
            ResultEditPreview.ResetView();
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await ViewModel.LoadImageAsync(files[0]);
        }
    }

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e) => ViewModel.GrabCut.SelectedRect = e;

    private void OriginalPreview_StrokeStart(object? sender, Point e) => ViewModel.OnOriginalStrokeStart(e);
    private void OriginalPreview_StrokeMove(object? sender, Point e) => ViewModel.OnOriginalStrokeMove(e);
    private void OriginalPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel.OnOriginalStrokeEnd();

    private void ResultEditPreview_StrokeStart(object? sender, Point e) => ViewModel.OnResultStrokeStart(e);
    private void ResultEditPreview_StrokeMove(object? sender, Point e) => ViewModel.OnResultStrokeMove(e);
    private void ResultEditPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel.OnResultStrokeEnd();
    private void ResultEditPreview_WandClicked(object? sender, OpenCvSharp.Point e) => ViewModel.OnResultWandClicked(e);

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e) => ViewModel.IsColorPickerOpen = !ViewModel.IsColorPickerOpen;
}
