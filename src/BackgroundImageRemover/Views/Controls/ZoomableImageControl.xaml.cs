using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Passive, read-only image preview with zoom and pan (no editing interactions). Mouse wheel
/// zooms toward the cursor, middle-drag pans, middle double-click (or the Fit button) resets,
/// and Ctrl+Plus/Minus/0/1 provide keyboard zoom while the control has focus.
/// </summary>
public partial class ZoomableImageControl : UserControl
{
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(BitmapSource), typeof(ZoomableImageControl),
            new PropertyMetadata(null, OnImageSourceChanged));

    public BitmapSource? ImageSource
    {
        get => (BitmapSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    private Point? _panStart;
    private Point _panStartTranslate;

    public ZoomableImageControl()
    {
        InitializeComponent();
        ZoomScale.Changed += (_, _) => UpdateZoomHud();
        ZoomPanHost.SizeChanged += (_, _) => UpdateZoomHud();
    }

    /// <summary>Restores the fit-to-window view.</summary>
    public void ResetView()
    {
        ZoomScale.ScaleX = 1;
        ZoomScale.ScaleY = 1;
        PanTranslate.X = 0;
        PanTranslate.Y = 0;
        UpdateZoomHud();
    }

    /// <summary>
    /// Number of image pixels per control DIP for the currently displayed bitmap at the
    /// fit-to-window zoom, used to compute the 1:1 view.
    /// </summary>
    public double ImagePixelScale
    {
        get
        {
            if (ImageSource is null)
            {
                return 1.0;
            }

            var content = CoordinateMapper.ImageControlContentRect(
                ZoomPanHost.ActualWidth, ZoomPanHost.ActualHeight,
                ImageSource.PixelWidth, ImageSource.PixelHeight);
            return content.Width > 0 ? ImageSource.PixelWidth / content.Width : 1.0;
        }
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || ImageSource is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomBy(1.1);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomBy(1.0 / 1.1);
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                ResetView();
                e.Handled = true;
                break;
            case Key.D1:
            case Key.NumPad1:
                ZoomActual();
                e.Handled = true;
                break;
        }
    }

    private void RootGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        var cursor = e.GetPosition(ZoomPanHost);
        if (ViewInteractionHelper.ComputeZoom(cursor, e.Delta, ZoomScale.ScaleX, new Point(PanTranslate.X, PanTranslate.Y), 1.0, 8.0, out var newScale, out var newTranslate))
        {
            ZoomScale.ScaleX = newScale;
            ZoomScale.ScaleY = newScale;
            PanTranslate.X = newTranslate.X;
            PanTranslate.Y = newTranslate.Y;
            e.Handled = true;
        }
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Give the control keyboard focus on click so the zoom shortcuts work immediately.
        RootGrid.Focus();

        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
            return;
        }

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _panStart = e.GetPosition(this);
            _panStartTranslate = new Point(PanTranslate.X, PanTranslate.Y);
            RootGrid.CaptureMouse();
            e.Handled = true;
        }
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is { } panStart && e.MiddleButton == MouseButtonState.Pressed)
        {
            var p = ViewInteractionHelper.ComputePan(panStart, _panStartTranslate, e.GetPosition(this));
            PanTranslate.X = p.X;
            PanTranslate.Y = p.Y;
            e.Handled = true;
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null && e.MiddleButton == MouseButtonState.Released)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        // A middle-drag that leaves the control would otherwise stay captured until the
        // button is released anywhere; release it when the cursor exits the control.
        if (_panStart is not null && e.MiddleButton == MouseButtonState.Released)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
        }
    }

    private void ZoomBy(double factor)
    {
        if (ImageSource is null)
        {
            return;
        }

        var center = new Point(ZoomPanHost.ActualWidth / 2, ZoomPanHost.ActualHeight / 2);
        int wheelDelta = factor >= 1 ? 120 : -120;
        if (ViewInteractionHelper.ComputeZoom(center, wheelDelta, ZoomScale.ScaleX, new Point(PanTranslate.X, PanTranslate.Y), 1.0, 8.0, out var newScale, out var newTranslate))
        {
            ZoomScale.ScaleX = newScale;
            ZoomScale.ScaleY = newScale;
            PanTranslate.X = newTranslate.X;
            PanTranslate.Y = newTranslate.Y;
            UpdateZoomHud();
        }
    }

    private void ZoomActual()
    {
        if (ImageSource is null)
        {
            return;
        }

        double oneToOne = ImagePixelScale;
        if (oneToOne <= 0)
        {
            return;
        }
        ZoomScale.ScaleX = oneToOne;
        ZoomScale.ScaleY = oneToOne;
        UpdateZoomHud();
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ResetView();

    private void ZoomActual_Click(object sender, RoutedEventArgs e) => ZoomActual();

    private void UpdateZoomHud()
    {
        if (ImageSource is null)
        {
            ZoomHud.Visibility = Visibility.Collapsed;
            return;
        }

        ZoomHud.Visibility = Visibility.Visible;
        ZoomPercentLabel.Text = $"{Math.Round(ZoomScale.ScaleX * 100)}%";
    }

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ZoomableImageControl)d;
        control.ResetView();
    }
}
