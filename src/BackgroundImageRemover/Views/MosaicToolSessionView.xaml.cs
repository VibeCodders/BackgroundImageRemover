using System.Windows;
using System.Windows.Controls;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.Views.Controls;

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
        => ViewModel?.OnBrushStrokeStart(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void MosaicPreview_StrokeMove(object? sender, Point e)
        => ViewModel?.OnBrushStrokeMove(e, BrushPixelRadius(sender, ViewModel.BrushRadius));

    private void MosaicPreview_StrokeEnd(object? sender, EventArgs e) => ViewModel?.OnBrushStrokeEnd();

    private void ChooseFillColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.IsFillColorPickerOpen = !vm.IsFillColorPickerOpen;
    }

    private static double BrushPixelRadius(object? sender, double fallback)
        => sender is ImagePreviewControl preview ? preview.BrushRadius * preview.ImagePixelScale : fallback;
}
