using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class FrameToolSessionView : UserControl
{
    private FrameToolSessionViewModel? ViewModel => DataContext as FrameToolSessionViewModel;

    public FrameToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseBorderColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsColorPickerOpen = !vm.IsColorPickerOpen;
    }

    private void ChooseInnerColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsInnerColorPickerOpen = !vm.IsInnerColorPickerOpen;
    }

    private void ChooseMatColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsMatColorPickerOpen = !vm.IsMatColorPickerOpen;
    }

    private void ChooseGradientColorAButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsGradientColorAPickerOpen = !vm.IsGradientColorAPickerOpen;
    }

    private void ChooseGradientColorBButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsGradientColorBPickerOpen = !vm.IsGradientColorBPickerOpen;
    }

    private void ChoosePolaroidColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsPolaroidColorPickerOpen = !vm.IsPolaroidColorPickerOpen;
    }

    private void ChooseVignetteColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsVignetteColorPickerOpen = !vm.IsVignetteColorPickerOpen;
    }
}
