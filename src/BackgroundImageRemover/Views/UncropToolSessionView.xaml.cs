using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class UncropToolSessionView : UserControl
{
    private UncropToolSessionViewModel? ViewModel => DataContext as UncropToolSessionViewModel;

    public UncropToolSessionView()
    {
        InitializeComponent();
    }

    private void UncropColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.IsColorPickerOpen = !ViewModel.IsColorPickerOpen;
        }
    }
}
