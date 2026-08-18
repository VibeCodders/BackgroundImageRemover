using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
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
        ViewInteractionHelper.HandleImageDragOver(e);
    }

    private async void UncropView_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not null && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ViewInteractionHelper.IsSupportedImage(files[0]))
        {
            await ViewModel.LoadAsync(files[0]);
        }
    }

    private void UncropColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.IsColorPickerOpen = !ViewModel.IsColorPickerOpen;
        }
    }
}
