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

        px = Math.Clamp(px, 0, bitmapPixelWidth);
        py = Math.Clamp(py, 0, bitmapPixelHeight);
        return new Point(px, py);
    }

    public static Rect ControlRectToImagePixelRect(Rect controlRect, double controlWidth, double controlHeight, int bitmapPixelWidth, int bitmapPixelHeight)
    {
        var topLeft = ControlPointToImagePixel(controlRect.TopLeft, controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
        var bottomRight = ControlPointToImagePixel(controlRect.BottomRight, controlWidth, controlHeight, bitmapPixelWidth, bitmapPixelHeight);
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
