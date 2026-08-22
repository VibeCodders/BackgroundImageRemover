using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="ColorReplaceToolSessionView"/>.</summary>
public partial class ColorReplaceToolSessionView : UserControl
{
    private ColorReplaceToolSessionViewModel? ViewModel => DataContext as ColorReplaceToolSessionViewModel;

    public ColorReplaceToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseTargetColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsTargetColorPickerOpen = !vm.IsTargetColorPickerOpen;
    }

    private void ChooseReplacementColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsReplacementColorPickerOpen = !vm.IsReplacementColorPickerOpen;
    }
}
