using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Views.Controls;

public partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(BitmapSource), typeof(ImagePreviewControl),
            new PropertyMetadata(null, OnImageSourceChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(InteractionMode), typeof(ImagePreviewControl),
            new PropertyMetadata(InteractionMode.None, OnModeChanged));

    public static readonly DependencyProperty BrushRadiusProperty =
        DependencyProperty.Register(nameof(BrushRadius), typeof(double), typeof(ImagePreviewControl),
            new PropertyMetadata(20.0));

    public BitmapSource? ImageSource
    {
        get => (BitmapSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public InteractionMode Mode
    {
        get => (InteractionMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Radius (in control DIPs) of the brush cursor preview circle.</summary>
    public double BrushRadius
    {
        get => (double)GetValue(BrushRadiusProperty);
        set => SetValue(BrushRadiusProperty, value);
    }

    /// <summary>Raised with the finalized selection rectangle, in source-image pixel coordinates.</summary>
    public event EventHandler<OpenCvSharp.Rect>? RectSelected;

    /// <summary>Raised at the start of a Brush/Scribble stroke, with the point in image-pixel coordinates.</summary>
    public event EventHandler<Point>? StrokeStart;

    /// <summary>Raised for each subsequent point of an in-progress stroke, in image-pixel coordinates.</summary>
    public event EventHandler<Point>? StrokeMove;

    public event EventHandler? StrokeEnd;

    /// <summary>Raised on a Magic Wand click, with the point in image-pixel coordinates.</summary>
    public event EventHandler<OpenCvSharp.Point>? WandClicked;

    private Point? _dragStart;
    private Point? _panStart;
    private Point _panStartTranslate;
    private Polyline? _activeStrokeVisual;

    // Scribble strokes (unlike Brush strokes) stay visible on the canvas, so their visuals
    // need their own undo/redo stack, kept in step with the ViewModel's scribble mask stack.
    private readonly Stack<Polyline> _scribbleUndoVisuals = new();
    private readonly Stack<Polyline> _scribbleRedoVisuals = new();

    public ImagePreviewControl()
    {
        InitializeComponent();
    }

    public bool UndoScribbleStroke()
    {
        if (_scribbleUndoVisuals.Count == 0)
        {
            return false;
        }
        var line = _scribbleUndoVisuals.Pop();
        OverlayCanvas.Children.Remove(line);
        _scribbleRedoVisuals.Push(line);
        return true;
    }

    public bool RedoScribbleStroke()
    {
        if (_scribbleRedoVisuals.Count == 0)
        {
            return false;
        }
        var line = _scribbleRedoVisuals.Pop();
        OverlayCanvas.Children.Add(line);
        _scribbleUndoVisuals.Push(line);
        return true;
    }

    public void ClearScribbleStrokes()
    {
        foreach (var line in _scribbleUndoVisuals)
        {
            OverlayCanvas.Children.Remove(line);
        }
        _scribbleUndoVisuals.Clear();
        _scribbleRedoVisuals.Clear();
    }

    public void ResetView()
    {
        ZoomScale.ScaleX = 1;
        ZoomScale.ScaleY = 1;
        PanTranslate.X = 0;
        PanTranslate.Y = 0;
    }

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ImagePreviewControl)d;
        control.ClearSelection();
        control.ClearScribbleStrokes();
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ImagePreviewControl)d).ClearSelection();

    private void ClearSelection()
    {
        _dragStart = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
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
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || ImageSource is null)
        {
            return;
        }

        switch (Mode)
        {
            case InteractionMode.DrawRect:
                StartRect(e);
                break;
            case InteractionMode.ScribbleForeground:
            case InteractionMode.ScribbleBackground:
            case InteractionMode.Brush:
                StartStroke(e);
                break;
            case InteractionMode.MagicWand:
                var imgPoint = ImagePixelAt(e);
                if (imgPoint is { } p)
                {
                    WandClicked?.Invoke(this, new OpenCvSharp.Point((int)Math.Round(p.X), (int)Math.Round(p.Y)));
                }
                break;
        }
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is { } panStart && e.MiddleButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(this);
            PanTranslate.X = _panStartTranslate.X + (current.X - panStart.X);
            PanTranslate.Y = _panStartTranslate.Y + (current.Y - panStart.Y);
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || ImageSource is null)
        {
            return;
        }

        switch (Mode)
        {
            case InteractionMode.DrawRect when _dragStart is not null:
                UpdateRect(e);
                break;
            case InteractionMode.ScribbleForeground:
            case InteractionMode.ScribbleBackground:
            case InteractionMode.Brush:
                if (_dragStart is not null)
                {
                    ContinueStroke(e);
                }
                break;
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null && e.MiddleButton == MouseButtonState.Released)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
            return;
        }

        if (_dragStart is null)
        {
            return;
        }

        switch (Mode)
        {
            case InteractionMode.DrawRect:
                FinishRect();
                break;
            case InteractionMode.ScribbleForeground:
            case InteractionMode.ScribbleBackground:
            case InteractionMode.Brush:
                FinishStroke();
                break;
        }
    }

    private Point? ImagePixelAt(MouseEventArgs e)
    {
        if (ImageSource is null)
        {
            return null;
        }
        var controlPoint = e.GetPosition(OverlayCanvas);
        return CoordinateMapper.ControlPointToImagePixel(
            controlPoint, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);
    }

    private void StartRect(MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayCanvas);
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _dragStart.Value.X);
        Canvas.SetTop(SelectionRectangle, _dragStart.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        OverlayCanvas.CaptureMouse();
    }

    private void UpdateRect(MouseEventArgs e)
    {
        var start = _dragStart!.Value;
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

    private void FinishRect()
    {
        OverlayCanvas.ReleaseMouseCapture();
        _dragStart = null;

        var controlRect = new Rect(
            Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle),
            SelectionRectangle.Width, SelectionRectangle.Height);

        if (controlRect.Width < 3 || controlRect.Height < 3 || ImageSource is null)
        {
            ClearSelection();
            return;
        }

        var imageRect = CoordinateMapper.ControlRectToImagePixelRect(
            controlRect, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);

        RectSelected?.Invoke(this, imageRect.ToCvRect());
    }

    private void StartStroke(MouseButtonEventArgs e)
    {
        var imgPoint = ImagePixelAt(e);
        if (imgPoint is not { } point)
        {
            return;
        }

        _dragStart = e.GetPosition(OverlayCanvas);
        OverlayCanvas.CaptureMouse();

        var strokeColor = Mode switch
        {
            InteractionMode.ScribbleForeground => System.Windows.Media.Brushes.LimeGreen,
            InteractionMode.ScribbleBackground => System.Windows.Media.Brushes.Red,
            _ => System.Windows.Media.Brushes.DeepSkyBlue
        };
        _activeStrokeVisual = new Polyline
        {
            Stroke = strokeColor,
            StrokeThickness = Mode == InteractionMode.Brush ? BrushRadius * 2 : 4,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = Mode == InteractionMode.Brush ? 0.35 : 0.8
        };
        _activeStrokeVisual.Points.Add(_dragStart.Value);
        OverlayCanvas.Children.Add(_activeStrokeVisual);

        if (Mode is InteractionMode.ScribbleForeground or InteractionMode.ScribbleBackground)
        {
            _scribbleUndoVisuals.Push(_activeStrokeVisual);
            _scribbleRedoVisuals.Clear();
        }

        StrokeStart?.Invoke(this, point);
    }

    private void ContinueStroke(MouseEventArgs e)
    {
        var imgPoint = ImagePixelAt(e);
        if (imgPoint is not { } point)
        {
            return;
        }

        _activeStrokeVisual?.Points.Add(e.GetPosition(OverlayCanvas));
        StrokeMove?.Invoke(this, point);
    }

    private void FinishStroke()
    {
        OverlayCanvas.ReleaseMouseCapture();
        _dragStart = null;

        if (Mode == InteractionMode.Brush && _activeStrokeVisual is not null)
        {
            OverlayCanvas.Children.Remove(_activeStrokeVisual);
        }
        _activeStrokeVisual = null;

        StrokeEnd?.Invoke(this, EventArgs.Empty);
    }
}
