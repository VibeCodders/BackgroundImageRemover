using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class ComposeToolSessionView : UserControl
{
    private ComposeToolSessionViewModel? ViewModel => DataContext as ComposeToolSessionViewModel;

    public ComposeToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseSolidColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsSolidColorPickerOpen = !vm.IsSolidColorPickerOpen;
    }

    private void ChooseGradientTopColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsGradientTopColorPickerOpen = !vm.IsGradientTopColorPickerOpen;
    }

    private void ChooseGradientBottomColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsGradientBottomColorPickerOpen = !vm.IsGradientBottomColorPickerOpen;
    }

    private void ChooseShadowColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsShadowColorPickerOpen = !vm.IsShadowColorPickerOpen;
    }
}
