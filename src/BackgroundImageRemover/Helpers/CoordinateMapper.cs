using System.Windows;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Maps points/rects between the DIP coordinate space of a Stretch="Uniform" Image control
/// and the pixel coordinate space of the bitmap it displays, accounting for letterboxing.
/// </summary>
public static class CoordinateMapper
{
    public static Rect ImageControlContentRect(double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        if (bitmapPixelWidth <= 0 || bitmapPixelHeight <= 0 || controlWidth <= 0 || controlHeight <= 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        double scale = Math.Min(controlWidth / bitmapPixelWidth, controlHeight / bitmapPixelHeight);
        double renderedWidth = bitmapPixelWidth * scale;
        double renderedHeight = bitmapPixelHeight * scale;
        double offsetX = (controlWidth - renderedWidth) / 2.0;
        double offsetY = (controlHeight - renderedHeight) / 2.0;

        return new Rect(offsetX, offsetY, renderedWidth, renderedHeight);
    }

    public static Point ControlPointToImagePixel(Point controlPoint, double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        var content = ImageControlContentRect(controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        if (content.Width <= 0 || content.Height <= 0)
        {
            return new Point(0, 0);
        }

        double scale = content.Width / bitmapPixelWidth;
        double px = (controlPoint.X - content.X) / scale;
        double py = (controlPoint.Y - content.Y) / scale;

        // Clamp to the last valid pixel (width - 1): mapping to exactly "width" would produce
        // an out-of-bounds coordinate for tools that round and index into the image (Magic
        // Wand seed, SAM prompt point, scribble endpoints, GrabCut rectangle).
        px = Math.Clamp(px, 0, Math.Max(0, bitmapPixelWidth - 1));
        py = Math.Clamp(py, 0, Math.Max(0, bitmapPixelHeight - 1));
        return new Point(px, py);
    }

    public static Rect ControlRectToImagePixelRect(Rect controlRect, double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        var topLeft = ControlPointToImagePixel(controlRect.TopLeft, controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        var bottomRight = ControlPointToImagePixel(controlRect.BottomRight, controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>Inverse of <see cref="ControlPointToImagePixel"/>: maps an image-pixel point to its position
    /// on the control, accounting for letterboxing. Ignores any additional zoom/pan transform (consistent
    /// with the other <see cref="ImagePreviewControl"/> mappings).</summary>
    public static Point ImagePixelToControlPoint(Point imagePoint, double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        var content = ImageControlContentRect(controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        if (content.Width <= 0 || content.Height <= 0 || bitmapPixelWidth <= 0 || bitmapPixelHeight <= 0)
        {
            return new Point(0, 0);
        }

        double scale = content.Width / bitmapPixelWidth;
        return new Point(content.X + imagePoint.X * scale, content.Y + imagePoint.Y * scale);
    }

    /// <summary>Maps an image-pixel rectangle to a control-space <see cref="Rect"/> (letterbox-aware).</summary>
    public static Rect ImageRectToControlRect(OpenCvSharp.Rect imageRect, double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        var topLeft = ImagePixelToControlPoint(new Point(imageRect.X, imageRect.Y), controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        var bottomRight = ImagePixelToControlPoint(new Point(imageRect.X + imageRect.Width, imageRect.Y + imageRect.Height), controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        return new Rect(topLeft, bottomRight);
    }

    public static OpenCvSharp.Rect ToCvRect(this Rect rect)
    {
        int x = (int)Math.Round(rect.X);
        int y = (int)Math.Round(rect.Y);
        int width = Math.Max(1, (int)Math.Round(rect.Width));
        int height = Math.Max(1, (int)Math.Round(rect.Height));
        return new OpenCvSharp.Rect(x, y, width, height);
    }
}
