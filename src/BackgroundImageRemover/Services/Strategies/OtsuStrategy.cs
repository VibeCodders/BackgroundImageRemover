using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes the background for high-contrast images (e.g. a dark product on a white backdrop).
/// Otsu's method picks the intensity threshold automatically; the side of the threshold that
/// dominates the image border is treated as the background, and the largest remaining connected
/// region is kept as the subject.
/// </summary>
public sealed class OtsuStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.Otsu;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        ct.ThrowIfCancellationRequested();

        // The background is the class that dominates the image border.
        using var foreground = new Mat();
        if (BorderIsMostlyBright(binary))
        {
            Cv2.BitwiseNot(binary, foreground); // subject is the dark side
        }
        else
        {
            binary.CopyTo(foreground); // subject is the bright side
        }

        var largest = KeepLargestFilledRegion(foreground);
        var feathered = new Mat();
        Cv2.GaussianBlur(largest, feathered, new Size(5, 5), 0);
        largest.Dispose();
        return feathered;
    }

    private static bool BorderIsMostlyBright(Mat binary)
    {
        int bright = 0;
        int dark = 0;

        for (int x = 0; x < binary.Width; x++)
        {
            Count(binary, x, 0, ref bright, ref dark);
            Count(binary, x, binary.Height - 1, ref bright, ref dark);
        }
        for (int y = 0; y < binary.Height; y++)
        {
            Count(binary, 0, y, ref bright, ref dark);
            Count(binary, binary.Width - 1, y, ref bright, ref dark);
        }

        return bright >= dark;
    }

    private static void Count(Mat binary, int x, int y, ref int bright, ref int dark)
    {
        if (binary.At<byte>(y, x) > 0)
        {
            bright++;
        }
        else
        {
            dark++;
        }
    }

    /// <summary>Keeps only the largest connected white region and fills any holes inside it.</summary>
    private static Mat KeepLargestFilledRegion(Mat binary)
    {
        Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var mask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.All(0));
        if (contours.Length == 0)
        {
            return mask;
        }

        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        // Drawing the outer contour filled also closes the holes inside the subject.
        IEnumerable<Point>[] single = { largest };
        Cv2.DrawContours(mask, single, -1, Scalar.All(255), thickness: -1);
        return mask;
    }
}
