using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="ShapeToolSessionView"/>.</summary>
public partial class ShapeToolSessionView : UserControl
{
    private ShapeToolSessionViewModel? ViewModel => DataContext as ShapeToolSessionViewModel;

    public ShapeToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseStrokeColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsStrokeColorPickerOpen = !vm.IsStrokeColorPickerOpen;
    }

    private void ChooseFillColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsFillColorPickerOpen = !vm.IsFillColorPickerOpen;
    }
}
