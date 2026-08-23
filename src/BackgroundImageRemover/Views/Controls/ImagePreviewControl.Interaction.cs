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
            case InteractionMode.EditRect:
                StartEdit(e);
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
            case InteractionMode.EditRect when _editGrab == EditGrab.New && _dragStart is not null:
                UpdateRect(e);
                break;
            case InteractionMode.EditRect when _editGrab is EditGrab.Move or EditGrab.ResizeTL or EditGrab.ResizeTR or EditGrab.ResizeBL or EditGrab.ResizeBR or EditGrab.Rotate:
                UpdateEdit(e);
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

        if (_dragStart is null && (Mode != InteractionMode.EditRect || _editGrab == EditGrab.None))
        {
            return;
        }

        switch (Mode)
        {
            case InteractionMode.DrawRect:
                FinishRect();
                break;
            case InteractionMode.EditRect:
                FinishEdit();
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

    /// <summary>Begins an EditRect gesture: drag outside the shape starts a new one, drag inside
    /// moves it, and dragging a corner handle resizes it.</summary>
    private void StartEdit(MouseButtonEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        var controlPoint = e.GetPosition(OverlayCanvas);
        _editGrabStart = controlPoint;

        if (_editImageRect is { } current)
        {
            var ctl = CoordinateMapper.ImageRectToControlRect(
                current, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
                ImageSource.PixelWidth, ImageSource.PixelHeight);
            _editGrabBaseControl = ctl;

            var rotateHandle = RotateHandleCenter(ctl, _editRotation);
            if (Near(controlPoint, rotateHandle, 12))
            {
                _editGrab = EditGrab.Rotate;
            }
            else
            {
                int corner = HitCorner(controlPoint, ctl);
                if (corner >= 0)
                {
                    _editGrab = EditGrab.ResizeTL + corner;
                }
                else if (ctl.Contains(controlPoint))
                {
                    _editGrab = EditGrab.Move;
                }
                else
                {
                    _editGrab = EditGrab.New;
                }
            }
        }
        else
        {
            _editGrab = EditGrab.New;
        }

        if (_editGrab == EditGrab.New)
        {
            StartRect(e);
        }
        else
        {
            RefreshEditView();
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void UpdateEdit(MouseEventArgs e)
    {
        if (ImageSource is null)
        {
            return;
        }

        var current = e.GetPosition(OverlayCanvas);

        if (_editGrab == EditGrab.Rotate && _editImageRect is { } rotated)
        {
            var ctl = CoordinateMapper.ImageRectToControlRect(
                rotated, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
                ImageSource.PixelWidth, ImageSource.PixelHeight);
            double angle = NormalizeDegrees(AngleFromCenter(ctl, current) + 90);

            // Hold Shift while dragging the rotation handle to snap to 5° steps
            // for precise alignments (0°, 5°, 10°, ... 355°).
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                angle = NormalizeDegrees(Math.Round(angle / 5.0) * 5.0);
            }

            _editRotation = angle;
            PositionRotateHandle(ctl, _editRotation);
            return;
        }

        Rect result;

        if (_editGrab == EditGrab.Move)
        {
            double dx = current.X - _editGrabStart.X;
            double dy = current.Y - _editGrabStart.Y;
            result = new Rect(
                _editGrabBaseControl.X + dx,
                _editGrabBaseControl.Y + dy,
                _editGrabBaseControl.Width,
                _editGrabBaseControl.Height);
        }
        var b = _editGrabBaseControl;
        double x0 = b.X, y0 = b.Y, x1 = b.Right, y1 = b.Bottom;
        switch (_editGrab)
        {
            case EditGrab.ResizeTR:
                x1 = current.X; y0 = current.Y; break;
            case EditGrab.ResizeBL:
                x0 = current.X; y1 = current.Y; break;
            case EditGrab.ResizeBR:
                x1 = current.X; y1 = current.Y; break;
            default: // ResizeTL
                x0 = current.X; y0 = current.Y; break;
        }
        result = new Rect(
            new Point(Math.Min(x0, x1), Math.Min(y0, y1)),
            new Point(Math.Max(x0, x1), Math.Max(y0, y1)));

        result = ClampControlRect(result);
        var img = CoordinateMapper.ControlRectToImagePixelRect(
            result, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);
        _editImageRect = img.ToCvRect();
        RefreshEditView();
    }

    private void FinishEdit()
    {
        if (_editGrab == EditGrab.None || ImageSource is null)
        {
            return;
        }

        bool wasNew = _editGrab == EditGrab.New;
        bool wasRotate = _editGrab == EditGrab.Rotate;
        _editGrab = EditGrab.None;
        OverlayCanvas.ReleaseMouseCapture();
        _dragStart = null;

        if (wasNew)
        {
            var controlRect = new Rect(
                Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle),
                SelectionRectangle.Width, SelectionRectangle.Height);
            if (controlRect.Width < 3 || controlRect.Height < 3)
            {
                ClearEditRect();
                return;
            }
            var imageRect = CoordinateMapper.ControlRectToImagePixelRect(
                controlRect, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
                ImageSource.PixelWidth, ImageSource.PixelHeight);
            _editImageRect = imageRect.ToCvRect();
            RefreshEditView();
        }

        if (wasRotate)
        {
            if (_editImageRect is { } rotateBase)
            {
                var rc = CoordinateMapper.ImageRectToControlRect(
                    rotateBase, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
                    ImageSource.PixelWidth, ImageSource.PixelHeight);
                PositionRotateHandle(rc, _editRotation);
            }
            RotationSelected?.Invoke(this, NormalizeDegrees(_editRotation));
        }
        else if (_editImageRect is { } finalRect)
        {
            if (wasNew)
            {
                // First draw in EditRect mode: treat like DrawRect for consumers.
                RectSelected?.Invoke(this, finalRect);
            }
            else
            {
                // Move/resize of existing rect: distinct event so consumers know it's an edit.
                EditRectSelected?.Invoke(this, finalRect);
                // Also fire RectSelected for backward compatibility (Shape tool sync).
                RectSelected?.Invoke(this, finalRect);
            }
        }
    }

    private static int HitCorner(Point controlPoint, Rect ctl)
    {
        double tol = 12;
        var corners = new[] { ctl.TopLeft, new Point(ctl.Right, ctl.Top), new Point(ctl.Left, ctl.Bottom), ctl.BottomRight };
        for (int i = 0; i < corners.Length; i++)
        {
            if (Math.Abs(controlPoint.X - corners[i].X) <= tol && Math.Abs(controlPoint.Y - corners[i].Y) <= tol)
            {
                return i;
            }
        }
        return -1;
    }

    private Rect ClampControlRect(Rect r)
    {
        if (ImageSource is null)
        {
            return r;
        }

        var content = CoordinateMapper.ImageControlContentRect(
            OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);
        double x = Math.Clamp(r.X, content.X, content.Right);
        double y = Math.Clamp(r.Y, content.Y, content.Bottom);
        double x1 = Math.Clamp(r.Right, content.X, content.Right);
        double y1 = Math.Clamp(r.Bottom, content.Y, content.Bottom);
        return new Rect(new Point(x, y), new Point(x1, y1));
    }

    private void RefreshEditView()
    {
        if (_editImageRect is not { } r || ImageSource is null || OverlayCanvas.ActualWidth <= 0)
        {
            return;
        }

        var ctl = CoordinateMapper.ImageRectToControlRect(
            r, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight,
            ImageSource.PixelWidth, ImageSource.PixelHeight);

        Canvas.SetLeft(SelectionRectangle, ctl.X);
        Canvas.SetTop(SelectionRectangle, ctl.Y);
        SelectionRectangle.Width = ctl.Width;
        SelectionRectangle.Height = ctl.Height;
        SelectionRectangle.Visibility = Visibility.Visible;

        PlaceHandle(EditHandleTL, ctl.TopLeft);
        PlaceHandle(EditHandleTR, new Point(ctl.Right, ctl.Top));
        PlaceHandle(EditHandleBL, new Point(ctl.Left, ctl.Bottom));
        PlaceHandle(EditHandleBR, ctl.BottomRight);
        PositionRotateHandle(ctl, _editRotation);
    }

    private static void PlaceHandle(System.Windows.Shapes.Shape handle, Point center)
    {
        Canvas.SetLeft(handle, center.X - handle.Width / 2);
        Canvas.SetTop(handle, center.Y - handle.Height / 2);
        handle.Visibility = Visibility.Visible;
    }

    private static Point RotateHandleCenter(Rect ctl, double rotation)
    {
        double radius = Math.Max(24, ctl.Height / 2 + 16);
        double ang = (-90 + rotation) * Math.PI / 180.0;
        double cx = ctl.X + ctl.Width / 2.0;
        double cy = ctl.Y + ctl.Height / 2.0;
        return new Point(cx + radius * Math.Cos(ang), cy + radius * Math.Sin(ang));
    }

    private static double AngleFromCenter(Rect ctl, Point p)
    {
        double cx = ctl.X + ctl.Width / 2.0;
        double cy = ctl.Y + ctl.Height / 2.0;
        return Math.Atan2(p.Y - cy, p.X - cx) * 180.0 / Math.PI; // -180..180; up = -90
    }

    private void PositionRotateHandle(Rect ctl, double rotation)
    {
        var handle = RotateHandleCenter(ctl, rotation);
        var center = new Point(ctl.X + ctl.Width / 2.0, ctl.Y + ctl.Height / 2.0);
        EditRotateLine.X1 = center.X; EditRotateLine.Y1 = center.Y;
        EditRotateLine.X2 = handle.X; EditRotateLine.Y2 = handle.Y;
        EditRotateLine.Visibility = Visibility.Visible;
        PlaceHandle(EditRotateHandle, handle);
    }

    private void HideEditHandles()
    {
        EditHandleTL.Visibility = Visibility.Collapsed;
        EditHandleTR.Visibility = Visibility.Collapsed;
        EditHandleBL.Visibility = Visibility.Collapsed;
        EditHandleBR.Visibility = Visibility.Collapsed;
        EditRotateLine.Visibility = Visibility.Collapsed;
        EditRotateHandle.Visibility = Visibility.Collapsed;
    }

    private static bool Near(Point a, Point b, double tol)
        => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;

    private static double NormalizeDegrees(double deg)
    {
        deg %= 360;
        if (deg < 0) deg += 360;
        return deg;
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
