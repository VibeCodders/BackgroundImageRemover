using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

    /// <summary>Raised on a SAM prompt click, with the point in image-pixel coordinates.</summary>
    public event EventHandler<OpenCvSharp.Point>? SamPointClicked;

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
        SamPointMarker.Visibility = Visibility.Collapsed;
    }
}
