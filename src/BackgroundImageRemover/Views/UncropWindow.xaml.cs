using System.Windows;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class UncropWindow : Window
{
    private UncropViewModel? ViewModel => DataContext as UncropViewModel;

    public UncropWindow(UncropViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }

    private void UncropColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.Options.IsColorPickerOpen = !ViewModel.Options.IsColorPickerOpen;
        }
    }
}
