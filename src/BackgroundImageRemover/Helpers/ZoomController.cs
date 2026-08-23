using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared zoom state machine for the image preview controls (ZoomableImageControl,
/// CompareImageControl, ImagePreviewControl). Owns the fit/zoom-in/zoom-out/actual-pixels
/// actions and the Ctrl+Plus/Minus/0/1 keyboard mapping that used to be copy-pasted into
/// each control. Varying parts (host size, image availability, 1:1 scale, HUD refresh) are
/// injected as delegates so the controls keep their own XAML wiring.
/// </summary>
public sealed class ZoomController
{
    private readonly ScaleTransform _scale;
    private readonly TranslateTransform _translate;
    private readonly Func<Size> _hostSize;
    private readonly Func<bool> _hasImage;
    private readonly Func<double> _imagePixelScale;
    private readonly Action _updateHud;
    private readonly double _minScale;
    private readonly double _maxScale;

    public ZoomController(
        ScaleTransform scale,
        TranslateTransform translate,
        Func<Size> hostSize,
        Func<bool> hasImage,
        Func<double> imagePixelScale,
        Action updateHud,
        double minScale = 1.0,
        double maxScale = 8.0)
    {
        _scale = scale;
        _translate = translate;
        _hostSize = hostSize;
        _hasImage = hasImage;
        _imagePixelScale = imagePixelScale;
        _updateHud = updateHud;
        _minScale = minScale;
        _maxScale = maxScale;
    }

    /// <summary>Restores the fit-to-window view.</summary>
    public void ResetView()
    {
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _translate.X = 0;
        _translate.Y = 0;
        _updateHud();
    }

    /// <summary>Zooms in/out centered on the viewport by the given factor.</summary>
    public void ZoomBy(double factor)
    {
        if (!_hasImage())
        {
            return;
        }

        var size = _hostSize();
        var center = new Point(size.Width / 2, size.Height / 2);
        int wheelDelta = factor >= 1 ? 120 : -120;
        if (ViewInteractionHelper.ComputeZoom(center, wheelDelta, _scale.ScaleX, new Point(_translate.X, _translate.Y), _minScale, _maxScale, out var newScale, out var newTranslate))
        {
            _scale.ScaleX = newScale;
            _scale.ScaleY = newScale;
            _translate.X = newTranslate.X;
            _translate.Y = newTranslate.Y;
            _updateHud();
        }
    }

    /// <summary>Shows the image at actual pixels (1 image pixel = 1 DIP).</summary>
    public void ZoomActual()
    {
        if (!_hasImage())
        {
            return;
        }

        double oneToOne = _imagePixelScale();
        if (oneToOne <= 0)
        {
            return;
        }
        _scale.ScaleX = oneToOne;
        _scale.ScaleY = oneToOne;
        _updateHud();
    }

    /// <summary>Zooms toward the cursor on a mouse-wheel delta. Returns true when handled.</summary>
    public bool HandleMouseWheel(Point cursor, int wheelDelta)
    {
        if (!_hasImage())
        {
            return false;
        }

        if (ViewInteractionHelper.ComputeZoom(cursor, wheelDelta, _scale.ScaleX, new Point(_translate.X, _translate.Y), _minScale, _maxScale, out var newScale, out var newTranslate))
        {
            _scale.ScaleX = newScale;
            _scale.ScaleY = newScale;
            _translate.X = newTranslate.X;
            _translate.Y = newTranslate.Y;
            return true;
        }

        return false;
    }

    /// <summary>Maps the Ctrl+Plus/Minus/0/1 zoom shortcuts. Returns true when the key was handled.</summary>
    public bool HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomBy(1.1);
                return true;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomBy(1.0 / 1.1);
                return true;
            case Key.D0:
            case Key.NumPad0:
                ResetView();
                return true;
            case Key.D1:
            case Key.NumPad1:
                ZoomActual();
                return true;
            default:
                return false;
        }
    }
}
