using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Views.Controls;

public partial class ImagePreviewControl
{
    private void RootGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        var cursor = e.GetPosition(OverlayCanvas);
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
        // Give the preview keyboard focus on click so the zoom shortcuts (Ctrl+Plus etc.)
        // work without the user having to tab to the control first.
        RootGrid.Focus();

        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
            return;
        }

        if (TryStartPan(e))
        {
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
            case InteractionMode.EraseForeground:
            case InteractionMode.EraseBackground:
            case InteractionMode.Brush:
            case InteractionMode.Lasso:
                StartStroke(e);
                break;
            case InteractionMode.MagicWand:
                var wandClickPoint = e.GetPosition(OverlayCanvas);
                var wandPoint = ImagePixelAt(e);
                if (wandPoint is { } wp)
                {
                    WandPointMarker.Visibility = Visibility.Visible;
                    Canvas.SetLeft(WandPointMarker, wandClickPoint.X - WandPointMarker.Width / 2);
                    Canvas.SetTop(WandPointMarker, wandClickPoint.Y - WandPointMarker.Height / 2);
                    WandClicked?.Invoke(this, new OpenCvSharp.Point((int)Math.Round(wp.X), (int)Math.Round(wp.Y)));
                }
                break;
            case InteractionMode.SamClick:
                var clickControlPoint = e.GetPosition(OverlayCanvas);
                var samPoint = ImagePixelAt(e);
                if (samPoint is { } sp)
                {
                    SamPointMarker.Visibility = Visibility.Visible;
                    Canvas.SetLeft(SamPointMarker, clickControlPoint.X - SamPointMarker.Width / 2);
                    Canvas.SetTop(SamPointMarker, clickControlPoint.Y - SamPointMarker.Height / 2);
                    SamPointClicked?.Invoke(this, new OpenCvSharp.Point((int)Math.Round(sp.X), (int)Math.Round(sp.Y)));
                }
                break;
        }
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        RaiseCursorImagePosition(e);
        UpdateBrushCursorHover(e);

        if (_panStart is { } panStart && IsPanButtonDown(e))
        {
            var p = ViewInteractionHelper.ComputePan(panStart, _panStartTranslate, e.GetPosition(this));
            PanTranslate.X = p.X;
            PanTranslate.Y = p.Y;
            e.Handled = true;
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
            case InteractionMode.EraseForeground:
            case InteractionMode.EraseBackground:
            case InteractionMode.Brush:
            case InteractionMode.Lasso:
                if (_dragStart is not null)
                {
                    ContinueStroke(e);
                }
                break;
        }
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        BrushCursorPreview.Visibility = Visibility.Collapsed;
        CursorImagePositionChanged?.Invoke(this, null);
    }

    /// <summary>Reports the image-pixel coordinates under the cursor (or null outside the image).</summary>
    private void RaiseCursorImagePosition(MouseEventArgs e)
    {
        if (CursorImagePositionChanged is null)
        {
            return;
        }

        if (ImagePixelAt(e) is { } p
            && ImageSource is not null
            && p.X >= 0 && p.Y >= 0
            && p.X < ImageSource.PixelWidth && p.Y < ImageSource.PixelHeight)
        {
            CursorImagePositionChanged(this, p);
        }
        else
        {
            CursorImagePositionChanged(this, null);
        }
    }

    /// <summary>Shows a brush-size circle under the cursor while hovering in Brush mode, so the
    /// user can see exactly where and how large the next stroke will be before painting.</summary>
    private void UpdateBrushCursorHover(MouseEventArgs e)
    {
        bool isBrush = Mode == InteractionMode.Brush;
        bool isErase = Mode is InteractionMode.EraseForeground or InteractionMode.EraseBackground;
        if ((!isBrush && !isErase) || ImageSource is null || _dragStart is not null || _panStart is not null)
        {
            BrushCursorPreview.Visibility = Visibility.Collapsed;
            return;
        }

        var p = e.GetPosition(OverlayCanvas);
        double diameter;
        if (isBrush)
        {
            diameter = Math.Max(4, BrushRadius * 2);
            BrushCursorPreview.Stroke = Brushes.DeepSkyBlue;
            BrushCursorPreview.Fill = new SolidColorBrush(Color.FromArgb(0x33, 0x0E, 0xA5, 0xE9));
            BrushCursorPreview.StrokeDashArray = null;
        }
        else
        {
            diameter = Math.Max(8, ScribbleManager.EraseThickness / ImagePixelScale);
            BrushCursorPreview.Stroke = Brushes.White;
            BrushCursorPreview.Fill = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            BrushCursorPreview.StrokeDashArray = new DoubleCollection { 3, 2 };
        }

        BrushCursorPreview.Width = diameter;
        BrushCursorPreview.Height = diameter;
        Canvas.SetLeft(BrushCursorPreview, p.X - diameter / 2);
        Canvas.SetTop(BrushCursorPreview, p.Y - diameter / 2);
        BrushCursorPreview.Visibility = Visibility.Visible;
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null && e.ChangedButton == _panButton)
        {
            _panStart = null;
            RootGrid.ReleaseMouseCapture();
            e.Handled = true;
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
            case InteractionMode.EraseForeground:
            case InteractionMode.EraseBackground:
            case InteractionMode.Brush:
            case InteractionMode.Lasso:
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

        // Scribble/eraser strokes are rendered from the ViewModel's scribble-mask overlay
        // (ScribbleOverlay), not as canvas polylines, so erasing and undo/redo always match
        // the actual masks. Only the transient Brush stroke keeps a temporary visual here.
        if (Mode == InteractionMode.Brush)
        {
            _activeStrokeVisual = new Polyline
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = BrushRadius * 2,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0.35
            };
            _activeStrokeVisual.Points.Add(_dragStart.Value);
            OverlayCanvas.Children.Add(_activeStrokeVisual);
        }
        else if (Mode == InteractionMode.Lasso)
        {
            // A thin traced outline, not a filled brush stroke -- the ViewModel closes the
            // polygon and fills it once the drag ends, so this is only a drawing guide.
            _activeStrokeVisual = new Polyline
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                StrokeLineJoin = PenLineJoin.Round
            };
            _activeStrokeVisual.Points.Add(_dragStart.Value);
            OverlayCanvas.Children.Add(_activeStrokeVisual);
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

        if (_activeStrokeVisual is not null)
        {
            OverlayCanvas.Children.Remove(_activeStrokeVisual);
        }
        _activeStrokeVisual = null;

        StrokeEnd?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts a pan when the gesture matches: middle-drag, right-drag, or Ctrl+left-drag.
    /// Returns true when a pan was started. Checked before the tool-specific handling so
    /// Ctrl+left-drag pans instead of drawing a rect, scribbling or brushing.
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
}
