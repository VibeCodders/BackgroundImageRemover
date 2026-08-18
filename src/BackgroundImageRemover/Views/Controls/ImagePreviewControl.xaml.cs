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

    /// <summary>
    /// Raised on mouse move with the image-pixel position under the cursor (null when the
    /// cursor is outside the image content), so the status bar can show live coordinates.
    /// </summary>
    public event EventHandler<Point?>? CursorImagePositionChanged;

    /// <summary>Raised when a single-letter shortcut activates a tool while this control has focus.</summary>
    public event EventHandler<EditorTool>? ToolShortcutInvoked;

    /// <summary>Raised when the W shortcut selects the Magic Wand strategy while this control has focus.</summary>
    public event EventHandler? MagicWandShortcutInvoked;

    private Point? _dragStart;
    private Point? _panStart;
    private Point _panStartTranslate;
    private Polyline? _activeStrokeVisual;

    public ImagePreviewControl()
    {
        InitializeComponent();
        ZoomScale.Changed += (_, _) => UpdateZoomHud();
        ZoomPanHost.SizeChanged += (_, _) => UpdateZoomHud();
    }

    public void ResetView()
    {
        ZoomScale.ScaleX = 1;
        ZoomScale.ScaleY = 1;
        PanTranslate.X = 0;
        PanTranslate.Y = 0;
        UpdateZoomHud();
    }

    /// <summary>Keeps the zoom HUD in sync: hidden until an image is shown, then showing the zoom percent.</summary>
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

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ResetView();

    /// <summary>
    /// Keyboard zoom shortcuts while the preview has focus: Ctrl+Plus / Ctrl+Minus zoom in/out
    /// centered on the viewport, Ctrl+0 fits the image, Ctrl+1 shows it at actual pixels.
    /// Public so the shortcut mapping can be exercised from unit tests.
    /// </summary>
    public void HandleZoomShortcut(Key key, bool controlPressed)
    {
        if (!controlPressed)
        {
            return;
        }

        switch (key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomBy(1.1);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomBy(1.0 / 1.1);
                break;
            case Key.D0:
            case Key.NumPad0:
                ResetView();
                break;
            case Key.D1:
            case Key.NumPad1:
                ZoomActual_Click(this, new RoutedEventArgs());
                break;
        }
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+... = zoom shortcuts; single letters (no modifiers) = tool shortcuts.
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            HandleZoomShortcut(e.Key, controlPressed: true);
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (PreviewToolShortcuts.ToolForKey(e.Key) is { } tool)
        {
            ToolShortcutInvoked?.Invoke(this, tool);
            e.Handled = true;
            return;
        }

        if (PreviewToolShortcuts.IsMagicWandKey(e.Key))
        {
            MagicWandShortcutInvoked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    /// <summary>Zooms the view around the center of the viewport by the given factor.</summary>
    private void ZoomBy(double factor)
    {
        if (ImageSource is null)
        {
            return;
        }

        var center = new Point(OverlayCanvas.ActualWidth / 2, OverlayCanvas.ActualHeight / 2);
        var currentTranslate = new Point(PanTranslate.X, PanTranslate.Y);
        int wheelDelta = factor >= 1 ? 120 : -120;
        if (ViewInteractionHelper.ComputeZoom(center, wheelDelta, ZoomScale.ScaleX, currentTranslate, 0.05, 32.0, out var newScale, out var newTranslate))
        {
            ZoomScale.ScaleX = newScale;
            ZoomScale.ScaleY = newScale;
            PanTranslate.X = newTranslate.X;
            PanTranslate.Y = newTranslate.Y;
            UpdateZoomHud();
        }
    }

    private void ZoomActual_Click(object sender, RoutedEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        // 1:1 means one image pixel per DIP: at ZoomScale == 1 the image is fit to the
        // control, so the required scale is the fit pixels-per-DIP ratio.
        double oneToOne = ImagePixelScale;
        if (oneToOne <= 0)
        {
            return;
        }
        ZoomScale.ScaleX = oneToOne;
        ZoomScale.ScaleY = oneToOne;
        UpdateZoomHud();
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
        control.UpdateZoomHud();
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
