using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

public static class AnchorPositionHelper
{
    public static Point ComputeOrigin(Size baseSize, Size blockSize, TextAnchor anchor, int margin)
    {
        int x = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.MiddleLeft or TextAnchor.BottomLeft => margin,
            TextAnchor.TopCenter or TextAnchor.Center or TextAnchor.BottomCenter => (baseSize.Width - blockSize.Width) / 2,
            _ => baseSize.Width - blockSize.Width - margin
        };

        int y = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.TopCenter or TextAnchor.TopRight => margin,
            TextAnchor.MiddleLeft or TextAnchor.Center or TextAnchor.MiddleRight => (baseSize.Height - blockSize.Height) / 2,
            _ => baseSize.Height - blockSize.Height - margin
        };

        return new Point(x, y);
    }

    public static (Rect Destination, Rect Overlay) ComputeIntersection(Size baseSize, Size overlaySize, TextAnchor anchor, int margin)
    {
        var pos = ComputeOrigin(baseSize, overlaySize, anchor, margin);
        int x = pos.X;
        int y = pos.Y;

        int ix0 = Math.Max(0, x);
        int iy0 = Math.Max(0, y);
        int ix1 = Math.Min(baseSize.Width, x + overlaySize.Width);
        int iy1 = Math.Min(baseSize.Height, y + overlaySize.Height);

        return (
            new Rect(ix0, iy0, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)),
            new Rect(ix0 - x, iy0 - y, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)));
    }
}
