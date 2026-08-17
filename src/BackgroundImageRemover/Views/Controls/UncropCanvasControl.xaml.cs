using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Shows the original image pinned inside the (possibly larger) padded Uncrop canvas, with 8
/// drag handles on the outer boundary. Dragging a handle updates <see cref="ImagePadding"/>, kept in
/// two-way sync with the window's aspect-preset buttons and numeric padding fields.
/// </summary>
public partial class UncropCanvasControl : UserControl
{
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(BitmapSource), typeof(UncropCanvasControl),
            new PropertyMetadata(null, OnVisualInputChanged));

    // Named ImagePadding (not "Padding") to avoid colliding with the Padding property Control
    // already declares (a Thickness, unrelated to this control's CanvasPadding state).
    public static readonly DependencyProperty ImagePaddingProperty =
        DependencyProperty.Register(nameof(ImagePadding), typeof(CanvasPadding), typeof(UncropCanvasControl),
            new PropertyMetadata(CanvasPadding.Zero, OnVisualInputChanged));

    public BitmapSource? ImageSource
    {
        get => (BitmapSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public CanvasPadding ImagePadding
    {
        get => (CanvasPadding)GetValue(ImagePaddingProperty);
        set => SetValue(ImagePaddingProperty, value);
    }

    private Point? _panStart;
    private Point _panStartTranslate;
    private double _contentScale = 1;

    public UncropCanvasControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    private static void OnVisualInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((UncropCanvasControl)d).Rebuild();

    private void OverlayCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        OverlayCanvas.Children.Clear();
        if (ImageSource is null || OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0)
        {
            return;
        }

        int imgW = ImageSource.PixelWidth;
        int imgH = ImageSource.PixelHeight;
        var padding = ImagePadding;
        double canvasPxW = imgW + padding.Left + padding.Right;
        double canvasPxH = imgH + padding.Top + padding.Bottom;

        var content = CoordinateMapper.ImageControlContentRect(
            OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight, (int)Math.Round(canvasPxW), (int)Math.Round(canvasPxH));
        if (content.Width <= 0 || content.Height <= 0)
        {
            return;
        }
        _contentScale = content.Width / canvasPxW;

        var background = new Rectangle
        {
            Width = content.Width,
            Height = content.Height,
            Fill = (Brush)FindResource("UncropNewAreaBrush")
        };
        Canvas.SetLeft(background, content.X);
        Canvas.SetTop(background, content.Y);
        OverlayCanvas.Children.Add(background);

        double imgDipW = imgW * _contentScale;
        double imgDipH = imgH * _contentScale;
        double imgLeft = content.X + padding.Left * _contentScale;
        double imgTop = content.Y + padding.Top * _contentScale;

        var image = new Image { Source = ImageSource, Width = imgDipW, Height = imgDipH, Stretch = Stretch.Fill };
        Canvas.SetLeft(image, imgLeft);
        Canvas.SetTop(image, imgTop);
        OverlayCanvas.Children.Add(image);

        var border = new Rectangle
        {
            Width = imgDipW,
            Height = imgDipH,
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 1.5,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, imgLeft);
        Canvas.SetTop(border, imgTop);
        OverlayCanvas.Children.Add(border);

        const double handleSize = 14;
        AddHandle("TopLeft", content.X, content.Y, handleSize);
        AddHandle("Top", content.X + content.Width / 2, content.Y, handleSize);
        AddHandle("TopRight", content.X + content.Width, content.Y, handleSize);
        AddHandle("Right", content.X + content.Width, content.Y + content.Height / 2, handleSize);
        AddHandle("BottomRight", content.X + content.Width, content.Y + content.Height, handleSize);
        AddHandle("Bottom", content.X + content.Width / 2, content.Y + content.Height, handleSize);
        AddHandle("BottomLeft", content.X, content.Y + content.Height, handleSize);
        AddHandle("Left", content.X, content.Y + content.Height / 2, handleSize);
    }

    private void AddHandle(string name, double centerX, double centerY, double size)
    {
        var thumb = new Thumb
        {
            Width = size,
            Height = size,
            Tag = name,
            Style = (Style)FindResource("UncropHandleStyle")
        };
        Canvas.SetLeft(thumb, centerX - size / 2);
        Canvas.SetTop(thumb, centerY - size / 2);
        thumb.DragDelta += Handle_DragDelta;
        OverlayCanvas.Children.Add(thumb);
    }

    private void Handle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb { Tag: string name } || ImageSource is null || _contentScale <= 0)
        {
            return;
        }

        // Thumb reports raw DIP deltas, unaware of the ZoomScale render transform above it, so
        // the delta has to be corrected back into content (padding-pixel) units by hand.
        double scale = _contentScale * ZoomScale.ScaleX;
        double dx = e.HorizontalChange / scale;
        double dy = e.VerticalChange / scale;

        var p = ImagePadding;
        int left = p.Left, top = p.Top, right = p.Right, bottom = p.Bottom;

        if (name.Contains("Left")) left = Math.Max(0, left - (int)Math.Round(dx));
        if (name.Contains("Right")) right = Math.Max(0, right + (int)Math.Round(dx));
        if (name.Contains("Top")) top = Math.Max(0, top - (int)Math.Round(dy));
        if (name.Contains("Bottom")) bottom = Math.Max(0, bottom + (int)Math.Round(dy));

        ImagePadding = new CanvasPadding(left, top, right, bottom);
    }

    private void RootGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        var cursor = e.GetPosition(OverlayCanvas);
        double oldScale = ZoomScale.ScaleX;
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        double newScale = Math.Clamp(oldScale * factor, 1.0, 8.0);
        if (Math.Abs(newScale - oldScale) < 1e-6)
        {
            return;
        }

        double px = (cursor.X - PanTranslate.X) / oldScale;
        double py = (cursor.Y - PanTranslate.Y) / oldScale;

        ZoomScale.ScaleX = newScale;
        ZoomScale.ScaleY = newScale;
        PanTranslate.X = cursor.X - px * newScale;
        PanTranslate.Y = cursor.Y - py * newScale;
        e.Handled = true;
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _panStart = e.GetPosition(this);
            _panStartTranslate = new Point(PanTranslate.X, PanTranslate.Y);
            RootGrid.CaptureMouse();
        }
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is { } start && e.MiddleButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(this);
            PanTranslate.X = _panStartTranslate.X + (current.X - start.X);
            PanTranslate.Y = _panStartTranslate.Y + (current.Y - start.Y);
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null && e.MiddleButton == MouseButtonState.Released)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
        }
    }
}
