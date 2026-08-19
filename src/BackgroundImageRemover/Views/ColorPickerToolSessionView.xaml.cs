using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

/// <summary>Code-behind for <see cref="ColorPickerToolSessionView"/>.</summary>
public partial class ColorPickerToolSessionView : UserControl
{
    public ColorPickerToolSessionView()
    {
        InitializeComponent();
    }

    private void ColorPickerPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ColorPickerToolSessionViewModel vm || ColorPickerPreview.ImageSource is null)
            return;

        var controlPoint = e.GetPosition(ColorPickerPreview);
        var imagePoint = CoordinateMapper.ControlPointToImagePixel(
            controlPoint,
            ColorPickerPreview.ActualWidth, ColorPickerPreview.ActualHeight,
            ColorPickerPreview.ImageSource.PixelWidth, ColorPickerPreview.ImageSource.PixelHeight);

        if (imagePoint is { } p)
        {
            vm.OnImageClicked(p);
        }
    }
}
