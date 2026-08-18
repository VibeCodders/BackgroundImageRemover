using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class TextToolSessionView : UserControl
{
    private TextToolSessionViewModel? ViewModel => DataContext as TextToolSessionViewModel;

    public TextToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsColorPickerOpen = !vm.IsColorPickerOpen;
    }
}
