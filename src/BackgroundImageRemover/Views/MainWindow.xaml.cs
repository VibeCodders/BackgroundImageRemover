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

    private void OriginalPreview_RectSelected(object? sender, OpenCvSharp.Rect e)
    {
        ViewModel.GrabCut.SelectedRect = e;
    }
}
