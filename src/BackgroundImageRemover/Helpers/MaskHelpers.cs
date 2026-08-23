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
}
