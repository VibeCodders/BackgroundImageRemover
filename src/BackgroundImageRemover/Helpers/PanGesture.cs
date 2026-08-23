using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared mouse-pan state machine used by the image preview controls (ImagePreviewControl,
/// ZoomableImageControl, CompareImageControl). A pan starts on middle-drag, right-drag or
/// Ctrl+left-drag, follows the mouse via <see cref="ViewInteractionHelper.ComputePan"/>,
/// and ends when the starting button is released (or is cancelled when the cursor leaves
/// the control mid-drag). The host wires it from its MouseDown/Move/Up/Leave handlers and
/// supplies the <see cref="TranslateTransform"/> to mutate and the element that captures
/// the mouse.
/// </summary>
public sealed class PanGesture
{
    private Point? _start;
    private Point _startTranslate;
    private MouseButton _button = MouseButton.Middle;

    /// <summary>True while a pan drag is in progress.</summary>
    public bool IsActive => _start is not null;

    /// <summary>
    /// Attempts to begin a pan from a mouse-down. Returns true (and marks the event handled)
    /// when the gesture matches; the caller should then skip its own tool-specific handling.
    /// </summary>
    public bool TryStart(MouseButtonEventArgs e, Point position, TranslateTransform translate, UIElement captureElement)
    {
        MouseButton? panButton = GetPanButton(e);
        if (panButton is not { } pb)
        {
            return false;
        }

        _button = pb;
        _start = position;
        _startTranslate = new Point(translate.X, translate.Y);
        captureElement.CaptureMouse();
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Applies the pan delta while the gesture is active. Returns true (and marks the event
    /// handled) when a pan was in progress.
    /// </summary>
    public bool Move(MouseEventArgs e, Point position, TranslateTransform translate)
    {
        if (_start is not { } start || !IsPanButtonDown(_button, e))
        {
            return false;
        }

        var p = ViewInteractionHelper.ComputePan(start, _startTranslate, position);
        translate.X = p.X;
        translate.Y = p.Y;
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Ends the pan when the button that started it is released. Returns true (and marks the
    /// event handled) when a pan was in progress.
    /// </summary>
    public bool End(MouseButtonEventArgs e, UIElement captureElement)
    {
        if (_start is null || e.ChangedButton != _button)
        {
            return false;
        }

        _start = null;
        captureElement.ReleaseMouseCapture();
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Cancels a pan drag that left the control while its button is still down, so the mouse
    /// capture does not linger until the button is released elsewhere.
    /// </summary>
    public void CancelIfButtonReleased(MouseEventArgs e, UIElement captureElement)
    {
        if (_start is not null && !IsPanButtonDown(_button, e))
        {
            _start = null;
            captureElement.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Returns the panning button for a mouse-down event: middle, right, or Ctrl+left.
    /// Null when the event does not start a pan.
    /// </summary>
    public static MouseButton? GetPanButton(MouseButtonEventArgs e)
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

    private static bool IsPanButtonDown(MouseButton button, MouseEventArgs e) => button switch
    {
        MouseButton.Left => e.LeftButton == MouseButtonState.Pressed,
        MouseButton.Right => e.RightButton == MouseButtonState.Pressed,
        _ => e.MiddleButton == MouseButtonState.Pressed,
    };
}
