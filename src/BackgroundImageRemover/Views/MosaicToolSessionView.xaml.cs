using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class MosaicToolSessionView : UserControl
{
    private MosaicToolSessionViewModel? ViewModel => DataContext as MosaicToolSessionViewModel;

    public MosaicToolSessionView()
    {
        InitializeComponent();
    }

    private void MosaicPreview_RectSelected(object? sender, OpenCvSharp.Rect e) => ViewModel?.OnRectSelected(e);

    private void MosaicPreview_StrokeStart(object? sender, Point e)
        => ViewModel?.OnBrushStrokeStart(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void MosaicPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, ViewInteractionHelper.BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void MosaicPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();

    private void ChooseFillColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsFillColorPickerOpen = !vm.IsFillColorPickerOpen;
    }
}
