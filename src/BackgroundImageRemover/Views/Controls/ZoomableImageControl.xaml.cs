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

    private readonly PanGesture _pan = new();
    private readonly ZoomController _zoom;

    public ZoomableImageControl()
    {
        InitializeComponent();
        ZoomScale.Changed += (_, _) => UpdateZoomHud();
        ZoomPanHost.SizeChanged += (_, _) => UpdateZoomHud();
        _zoom = new ZoomController(
            ZoomScale,
            PanTranslate,
            () => new Size(ZoomPanHost.ActualWidth, ZoomPanHost.ActualHeight),
            () => ImageSource is not null,
            () => ImagePixelScale,
            UpdateZoomHud);
    }

    /// <summary>Restores the fit-to-window view.</summary>
    public void ResetView() => _zoom.ResetView();

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

        if (_zoom.HandleKeyDown(e.Key))
        {
            e.Handled = true;
        }
    }

    private void RootGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_zoom.HandleMouseWheel(e.GetPosition(ZoomPanHost), e.Delta))
        {
            e.Handled = true;
        }
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Give the control keyboard focus on click so the zoom shortcuts work immediately.
        RootGrid.Focus();

        // Middle and right double-click both reset the view (right is equivalent to middle
        // for panning/reset interactions).
        if ((e.ChangedButton is MouseButton.Middle or MouseButton.Right) && e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
            return;
        }

        _pan.TryStart(e, e.GetPosition(this), PanTranslate, RootGrid);
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        _pan.Move(e, e.GetPosition(this), PanTranslate);
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _pan.End(e, RootGrid);
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        // A pan drag that leaves the control would otherwise stay captured until the
        // button is released anywhere; release it when the cursor exits the control.
        _pan.CancelIfButtonReleased(e, RootGrid);
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ResetView();

    private void ZoomActual_Click(object sender, RoutedEventArgs e) => _zoom.ZoomActual();

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
