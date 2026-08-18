using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views.Panels;

public partial class UncropPanel : UserControl
{
    public UncropPanel()
    {
        InitializeComponent();
    }

    private void ChooseUncropColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DocumentViewModel vm)
        {
            vm.UncropOptions.IsColorPickerOpen = !vm.UncropOptions.IsColorPickerOpen;
        }
    }
}
