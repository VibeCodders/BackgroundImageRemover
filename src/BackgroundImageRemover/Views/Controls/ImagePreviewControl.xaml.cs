using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;

namespace BackgroundImageRemover.Views.Controls;

public partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(BitmapSource), typeof(ImagePreviewControl),
            new PropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty IsRectDrawingEnabledProperty =
        DependencyProperty.Register(nameof(IsRectDrawingEnabled), typeof(bool), typeof(ImagePreviewControl),
            new PropertyMetadata(false, OnRectDrawingEnabledChanged));

    public BitmapSource? ImageSource
    {
        get => (BitmapSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public bool IsRectDrawingEnabled
    {
        get => (bool)GetValue(IsRectDrawingEnabledProperty);
        set => SetValue(IsRectDrawingEnabledProperty, value);
    }

    /// <summary>Raised with the finalized selection rectangle, in source-image pixel coordinates.</summary>
    public event EventHandler<OpenCvSharp.Rect>? RectSelected;

    private Point? _dragStart;

    public ImagePreviewControl()
    {
        InitializeComponent();
    }

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ImagePreviewControl)d).ClearSelection();
    }

    private static void OnRectDrawingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            ((ImagePreviewControl)d).ClearSelection();
        }
    }

    private void ClearSelection()
    {
        _dragStart = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
    }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        _dragStart = e.GetPosition(OverlayCanvas);
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _dragStart.Value.X);
        Canvas.SetTop(SelectionRectangle, _dragStart.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);
        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        double width = Math.Abs(current.X - start.X);
        double height = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null || ImageSource is null)
        {
            return;
        }

        OverlayCanvas.ReleaseMouseCapture();
        _dragStart = null;

        var controlRect = new Rect(
            Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle),
            SelectionRectangle.Width, SelectionRectangle.Height);

        if (controlRect.Width < 3 || controlRect.Height < 3)
        {
            ClearSelection();
            return;
        }

        var imageRect = CoordinateMapper.ControlRectToImagePixelRect(
            controlRect, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);

        RectSelected?.Invoke(this, imageRect.ToCvRect());
    }
}
