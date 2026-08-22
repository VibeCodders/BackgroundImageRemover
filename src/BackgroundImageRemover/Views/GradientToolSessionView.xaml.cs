using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="GradientToolSessionView"/>.</summary>
public partial class GradientToolSessionView : UserControl
{
    private GradientToolSessionViewModel? ViewModel => DataContext as GradientToolSessionViewModel;

    public GradientToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseColorAButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsColorAPickerOpen = !vm.IsColorAPickerOpen;
    }

    private void ChooseColorBButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsColorBPickerOpen = !vm.IsColorBPickerOpen;
    }
}
