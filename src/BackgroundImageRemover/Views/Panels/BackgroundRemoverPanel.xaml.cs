using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views.Panels;

public partial class BackgroundRemoverPanel : UserControl
{
    public BackgroundRemoverPanel()
    {
        InitializeComponent();
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DocumentViewModel vm)
        {
            vm.IsColorPickerOpen = !vm.IsColorPickerOpen;
        }
    }
}
