using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="PenToolSessionView"/>.</summary>
public partial class PenToolSessionView : UserControl
{
    private PenToolSessionViewModel? ViewModel => DataContext as PenToolSessionViewModel;

    public PenToolSessionView()
    {
        InitializeComponent();
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsColorPickerOpen = !vm.IsColorPickerOpen;
    }

    private void PenPreview_StrokeStart(object? sender, System.Windows.Point e)
        => ViewModel?.OnStrokeStart(e, BackgroundImageRemover.Helpers.ViewInteractionHelper.BrushPixelRadius(sender, ViewModel?.PenWidth ?? 6));

    private void PenPreview_StrokeMove(object? sender, System.Windows.Point e)
        => ViewModel?.OnStrokeMove(e, BackgroundImageRemover.Helpers.ViewInteractionHelper.BrushPixelRadius(sender, ViewModel?.PenWidth ?? 6));

    private void PenPreview_StrokeEnd(object? sender, System.EventArgs e) => ViewModel?.OnStrokeEnd();
}
