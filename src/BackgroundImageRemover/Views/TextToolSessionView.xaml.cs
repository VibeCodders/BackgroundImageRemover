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

    private void ChooseOutlineColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsOutlineColorPickerOpen = !vm.IsOutlineColorPickerOpen;
    }

    private void ChoosePlateColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsPlateColorPickerOpen = !vm.IsPlateColorPickerOpen;
    }

    private void ChooseShadowColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsShadowColorPickerOpen = !vm.IsShadowColorPickerOpen;
    }
}
