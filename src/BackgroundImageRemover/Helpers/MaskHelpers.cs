using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared single-channel mask post-processing used by the background-removal strategies:
/// the repeated "blur into a new feathered mask and release the source" tail and the
/// "keep only the largest connected region, holes filled" contour step that Otsu and
/// EdgeContour previously implemented twice.
/// </summary>
public static class MaskHelpers
{
    /// <summary>
    /// Blurs <paramref name="mask"/> with a <paramref name="kernelSize"/>×kernel Gaussian
    /// kernel into a new Mat and returns it. <b>Takes ownership of <paramref name="mask"/></b>
    /// and disposes it, mirroring the pattern every strategy used ("blur, then dispose the
    /// raw mask"). The caller must not use <paramref name="mask"/> afterwards.
    /// </summary>
    public static Mat Feather(Mat mask, int kernelSize = 5)
    {
        kernelSize = EditingGuard.EnsureOdd(Math.Max(1, kernelSize));
        var feathered = new Mat();
        Cv2.GaussianBlur(mask, feathered, new Size(kernelSize, kernelSize), 0);
        mask.Dispose();
        return feathered;
    }

    /// <summary>
    /// Keeps only the largest connected white region of <paramref name="binary"/> and fills
    /// any holes inside it (drawing the outer contour filled closes interior gaps). Returns a
    /// new mask; <paramref name="binary"/> is left untouched.
    /// </summary>
    public static Mat KeepLargestFilledRegion(Mat binary)
    {
        Cv2.FindContours(binary, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var mask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
        if (contours.Length == 0)
        {
            return mask;
        }

        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        IEnumerable<Point>[] single = { largest };
        Cv2.DrawContours(mask, single, -1, Scalar.All(255), thickness: -1);
        return mask;
    }

    /// <summary>
    /// Flood-fills <paramref name="bgr"/> in Lab space (so <paramref name="tolerance"/> behaves
    /// as a perceptual color distance) from each seed on a shared mask and returns the interior
    /// mask with the flooded pixels set to 255. This is the repeated "flood the background from
    /// the border" setup used by FloodFill, MagicWand and Inpaint — including the 2px larger
    /// working mask OpenCV's FloodFill requires.
    /// </summary>
    public static Mat FloodFillBorderMask(Mat bgr, Point[] seeds, double tolerance)
    {
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);

        var diff = new Scalar(Math.Max(1, tolerance));
        var flags = FloodFillFlags.Link8 | FloodFillFlags.MaskOnly | (FloodFillFlags)(255 << 8);

        // FloodFill's mask must be 2px larger than the image on every side.
        using var floodMask = new Mat(bgr.Height + 2, bgr.Width + 2, MatType.CV_8UC1, Scalar.All(0));
        foreach (var seed in seeds)
        {
            Cv2.FloodFill(lab, floodMask, seed, Scalar.All(255), out _, diff, diff, flags);
        }

        using var interior = new Mat(floodMask, new Rect(1, 1, bgr.Width, bgr.Height));
        var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        interior.CopyTo(mask);
        return mask;
    }
}
