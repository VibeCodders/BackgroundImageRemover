using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Halftone: renders the image as dots whose size follows the local luminance.</summary>
public static class HalftoneService
{
    /// <summary>
    /// Renders <paramref name="bgr"/> in a halftone style: each <paramref name="cellSize"/>×cell
    /// region becomes a filled <paramref name="dotColor"/> circle on white whose radius is
    /// proportional to the region's darkness (light regions → tiny or no dot). <paramref name="invert"/>
    /// flips the mapping so light regions get the big dots. The caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, int cellSize, Vec3b dotColor, bool invert)
    {
        cellSize = Math.Clamp(cellSize, 2, 16);

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        var result = new Mat(bgr.Size(), MatType.CV_8UC3, Scalar.All(255));
        float maxRadius = cellSize * 0.5f;
        double maxDotR = Math.Max(1.0, maxRadius);
        var graySpan = gray.AsSpan2D<byte>();

        for (int cy = cellSize / 2; cy < bgr.Height; cy += cellSize)
        {
            for (int cx = cellSize / 2; cx < bgr.Width; cx += cellSize)
            {
                // Average luminance of the cell (clamped at the borders).
                int x0 = Math.Max(0, cx - cellSize / 2);
                int y0 = Math.Max(0, cy - cellSize / 2);
                int x1 = Math.Min(bgr.Width, cx + (cellSize - cellSize / 2));
                int y1 = Math.Min(bgr.Height, cy + (cellSize - cellSize / 2));
                double lum = 0;
                long n = 0;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        lum += graySpan[y, x];
                        n++;
                    }
                }

                if (n == 0)
                {
                    continue;
                }

                lum /= n;
                double darkness = 1.0 - lum / 255.0;
                if (invert)
                {
                    darkness = 1.0 - darkness;
                }

                double r = maxDotR * darkness;
                if (r >= 0.5)
                {
                    var color = new Scalar(dotColor.Item0, dotColor.Item1, dotColor.Item2);
                    Cv2.Circle(result, new Point(cx, cy), (int)Math.Round(r), color, thickness: -1,
                        LineTypes.AntiAlias);
                }
            }
        }

        return result;
    }
}
