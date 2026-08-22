using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes the background by finding the subject's outline: Canny edge detection, morphological
/// closing to bridge small gaps into a continuous boundary, then filling the largest closed
/// region. Works best on subjects with a clear silhouette against a comparatively uncluttered
/// background -- a classic-CV alternative to Otsu for images where the subject isn't simply the
/// darker/brighter side of a single threshold (e.g. a textured subject on a plain backdrop).
/// </summary>
public sealed class EdgeContourStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.EdgeContour;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 150);
        ct.ThrowIfCancellationRequested();

        // Bridge small gaps in the edge outline so the subject's boundary forms a closed loop
        // that FindContours can fill, then thicken it slightly so thin gaps close too.
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using var closed = new Mat();
        Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel, iterations: 2);
        using var dilated = new Mat();
        Cv2.Dilate(closed, dilated, kernel, iterations: 1);
        ct.ThrowIfCancellationRequested();

        using var largest = KeepLargestFilledRegion(dilated);
        var feathered = new Mat();
        Cv2.GaussianBlur(largest, feathered, new Size(5, 5), 0);
        return feathered;
    }

    /// <summary>Keeps only the largest connected white region and fills any holes inside it.</summary>
    private static Mat KeepLargestFilledRegion(Mat binary)
    {
        Cv2.FindContours(binary, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var mask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
        if (contours.Length == 0)
        {
            return mask;
        }

        var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        IEnumerable<Point>[] single = { largestContour };
        Cv2.DrawContours(mask, single, -1, Scalar.All(255), thickness: -1);
        return mask;
    }
}
