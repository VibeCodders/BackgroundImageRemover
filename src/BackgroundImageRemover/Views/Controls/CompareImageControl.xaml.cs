using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Before/after reveal slider: "After" is drawn over "Before", clipped to the divider position.
/// Supports the same zoom/pan as the other previews: wheel zooms toward the cursor, middle-drag
/// pans, middle double-click resets, Ctrl+Plus/Minus/0/1 while focused.
/// </summary>
public partial class CompareImageControl : UserControl
{
    public static readonly DependencyProperty BeforeSourceProperty =
        DependencyProperty.Register(nameof(BeforeSource), typeof(BitmapSource), typeof(CompareImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AfterSourceProperty =
        DependencyProperty.Register(nameof(AfterSource), typeof(BitmapSource), typeof(CompareImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DividerPositionProperty =
        DependencyProperty.Register(nameof(DividerPosition), typeof(double), typeof(CompareImageControl),
            new FrameworkPropertyMetadata(0.5,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnDividerPositionChanged));

    public BitmapSource? BeforeSource
    {
        get => (BitmapSource?)GetValue(BeforeSourceProperty);
        set => SetValue(BeforeSourceProperty, value);
    }

    public BitmapSource? AfterSource
    {
        get => (BitmapSource?)GetValue(AfterSourceProperty);
        set => SetValue(AfterSourceProperty, value);
    }

    /// <summary>0 = fully "before", 1 = fully "after".</summary>
    public double DividerPosition
    {
        get => (double)GetValue(DividerPositionProperty);
        set => SetValue(DividerPositionProperty, value);
    }

    private Point? _panStart;
    private Point _panStartTranslate;
    private MouseButton _panButton = MouseButton.Middle;
    private bool _draggingDivider;

    public CompareImageControl()
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
            var source = AfterSource ?? BeforeSource;
            if (source is null)
            {
                return 1.0;
            }

            var content = CoordinateMapper.ImageControlContentRect(
                ZoomPanHost.ActualWidth, ZoomPanHost.ActualHeight,
                source.PixelWidth, source.PixelHeight);
            return content.Width > 0 ? source.PixelWidth / content.Width : 1.0;
        }
    }

    private static void OnDividerPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CompareImageControl)d).UpdateClip();

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateClip();

    private void UpdateClip()
    {
        double width = ZoomPanHost.ActualWidth;
        double height = ZoomPanHost.ActualHeight;
        double dividerX = width * Math.Clamp(DividerPosition, 0.0, 1.0);

        AfterClip.Rect = new Rect(0, 0, dividerX, height);
        DividerLine.Height = height;
        DividerLine.Margin = new Thickness(dividerX, 0, 0, 0);
        DividerThumb.Margin = new Thickness(dividerX - 10, 0, 0, 0);
    }

    private void DividerThumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _draggingDivider = true;
        DividerThumb.CaptureMouse();
        UpdateDividerFromMouse(e);
        e.Handled = true;
    }

    private void DividerThumb_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingDivider)
        {
            return;
        }

        UpdateDividerFromMouse(e);
        e.Handled = true;
    }

    private void DividerThumb_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _draggingDivider = false;
        DividerThumb.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>
    /// Positions the divider from the mouse in the (pre-transform) host space, so it tracks
    /// the image content exactly even while zoomed or panned.
    /// </summary>
    private void UpdateDividerFromMouse(MouseEventArgs e)
    {
        double width = ZoomPanHost.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        double localX = e.GetPosition(ZoomPanHost).X;
        DividerPosition = Math.Clamp(localX / width, 0.0, 1.0);
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || ImageSourceAvailable() is false)
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
        if (!ImageSourceAvailable())
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
        RootGrid.Focus();

        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
            return;
        }

        TryStartPan(e);
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is { } panStart && IsPanButtonDown(e))
        {
            var p = ViewInteractionHelper.ComputePan(panStart, _panStartTranslate, e.GetPosition(this));
            PanTranslate.X = p.X;
            PanTranslate.Y = p.Y;
            e.Handled = true;
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null && e.ChangedButton == _panButton)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_panStart is not null && !IsPanButtonDown(e))
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Starts a pan when the gesture matches: middle-drag, right-drag, or Ctrl+left-drag.
    /// Returns true when a pan was started.
    /// </summary>
    private bool TryStartPan(MouseButtonEventArgs e)
    {
        MouseButton? panButton = GetPanButton(e);
        if (panButton is not { } pb)
        {
            return false;
        }

        _panButton = pb;
        _panStart = e.GetPosition(this);
        _panStartTranslate = new Point(PanTranslate.X, PanTranslate.Y);
        RootGrid.CaptureMouse();
        e.Handled = true;
        return true;
    }

    private static MouseButton? GetPanButton(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            return MouseButton.Middle;
        }
        if (e.ChangedButton == MouseButton.Right)
        {
            return MouseButton.Right;
        }
        if (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return MouseButton.Left;
        }
        return null;
    }

    private bool IsPanButtonDown(MouseEventArgs e) => _panButton switch
    {
        MouseButton.Left => e.LeftButton == MouseButtonState.Pressed,
        MouseButton.Right => e.RightButton == MouseButtonState.Pressed,
        _ => e.MiddleButton == MouseButtonState.Pressed,
    };

    private bool ImageSourceAvailable() => AfterSource is not null || BeforeSource is not null;

    private void ZoomBy(double factor)
    {
        if (!ImageSourceAvailable())
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
        if (!ImageSourceAvailable())
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
        if (!ImageSourceAvailable())
        {
            ZoomHud.Visibility = Visibility.Collapsed;
            return;
        }

        ZoomHud.Visibility = Visibility.Visible;
        ZoomPercentLabel.Text = $"{Math.Round(ZoomScale.ScaleX * 100)}%";
    }
}
