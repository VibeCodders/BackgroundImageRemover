using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Edge detection utilities for color decontamination operations.
/// </summary>
internal static class EdgeDetection
{
    /// <summary>
    /// Finds the bounding box of semi-transparent (1..254 alpha) pixels, with the corresponding binary mask.
    /// Returns null when there is nothing to decontaminate.
    /// </summary>
    public static Rect? FindEdgeBand(Mat alpha, out Mat edgeMask)
    {
        edgeMask = new Mat();
        Cv2.InRange(alpha, new Scalar(1), new Scalar(254), edgeMask);

        using var nonZero = new Mat();
        Cv2.FindNonZero(edgeMask, nonZero);
        if (nonZero.Rows == 0)
        {
            edgeMask.Dispose();
            edgeMask = null!;
            return null;
        }

        return Cv2.BoundingRect(nonZero);
    }

    /// <summary>
    /// Grows a rectangle by a margin pixels, clamped to the image bounds.
    /// </summary>
    public static Rect ExpandRect(Rect rect, int margin, Size bounds)
    {
        int x = Math.Max(0, rect.X - margin);
        int y = Math.Max(0, rect.Y - margin);
        int right = Math.Min(bounds.Width, rect.X + rect.Width + margin);
        int bottom = Math.Min(bounds.Height, rect.Y + rect.Height + margin);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }
}