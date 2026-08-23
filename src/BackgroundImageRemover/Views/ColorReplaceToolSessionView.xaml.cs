using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BackgroundImageRemover.Helpers;
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

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is not { } vm || Preview.ImageSource is null)
        {
            return;
        }

        var controlPoint = e.GetPosition(Preview);
        var imagePoint = CoordinateMapper.ControlPointToImagePixel(
            controlPoint,
            Preview.ActualWidth, Preview.ActualHeight,
            Preview.ImageSource.PixelWidth, Preview.ImageSource.PixelHeight);

        if (imagePoint is { } p)
        {
            vm.OnImageClicked(p);
        }
    }
}
