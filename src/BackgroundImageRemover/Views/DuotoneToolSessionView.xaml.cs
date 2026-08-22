using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="DuotoneToolSessionView"/>.</summary>
public partial class DuotoneToolSessionView : UserControl
{
    private DuotoneToolSessionViewModel? ViewModel => DataContext as DuotoneToolSessionViewModel;

    public DuotoneToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseDarkColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsDarkColorPickerOpen = !vm.IsDarkColorPickerOpen;
    }

    private void ChooseLightColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsLightColorPickerOpen = !vm.IsLightColorPickerOpen;
    }
}
