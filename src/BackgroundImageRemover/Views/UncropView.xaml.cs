using System.IO;
using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class UncropView : UserControl
{
    private UncropViewModel? ViewModel => DataContext as UncropViewModel;

    public UncropView()
    {
        InitializeComponent();
    }

    private void UncropView_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 } && IsSupportedImage(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void UncropView_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 } && IsSupportedImage(files[0]))
            {
                await ViewModel.LoadAsync(files[0]);
            }
        }
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp";
    }
}
