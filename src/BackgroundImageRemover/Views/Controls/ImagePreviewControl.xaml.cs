using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public static readonly DependencyProperty ScribbleOverlayProperty =
        DependencyProperty.Register(nameof(ScribbleOverlay), typeof(BitmapSource), typeof(ImagePreviewControl),
            new PropertyMetadata(null));

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

    /// <summary>
    /// Semi-transparent overlay (foreground/background scribbles) rendered above the image
    /// and aligned with it via the same Uniform stretch.
    /// </summary>
    public BitmapSource? ScribbleOverlay
    {
        get => (BitmapSource?)GetValue(ScribbleOverlayProperty);
        set => SetValue(ScribbleOverlayProperty, value);
    }

    /// <summary>
    /// Number of image pixels per control DIP for the currently displayed bitmap. The brush
    /// radius is specified in DIPs (it matches the on-screen cursor circle), so the actual
    /// alpha stamp must multiply it by this factor to land on the right image pixels.
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
                OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
                ImageSource.PixelWidth, ImageSource.PixelHeight);
            return content.Width > 0 ? ImageSource.PixelWidth / content.Width : 1.0;
        }
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

    public ImagePreviewControl()
    {
        InitializeComponent();
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

        // The brush recomposites the result bitmap on every stamp, which swaps this control's
        // ImageSource mid-stroke. Do not tear down the in-progress drag state here: clearing
        // _dragStart would abort the stroke and leave the mouse captured forever, blocking
        // every other click in the window.
        if (control._dragStart is not null)
        {
            return;
        }

        control.ClearSelection();
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ImagePreviewControl)d;
        control.ClearSelection();

        // Give interactive tools a crosshair cursor so the image area reads as editable,
        // while the plain arrow is kept for the default (non-tool) state.
        control.RootGrid.Cursor = control.Mode == InteractionMode.None ? Cursors.Arrow : Cursors.Cross;
    }

    private void ClearSelection()
    {
        _dragStart = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        SamPointMarker.Visibility = Visibility.Collapsed;
        WandPointMarker.Visibility = Visibility.Collapsed;
        BrushCursorPreview.Visibility = Visibility.Collapsed;
    }
}
