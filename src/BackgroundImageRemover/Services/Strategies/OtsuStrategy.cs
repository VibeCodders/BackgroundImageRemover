using BackgroundImageRemover.Helpers;
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

        return MaskHelpers.Feather(MaskHelpers.KeepLargestFilledRegion(foreground));
    }

    private static bool BorderIsMostlyBright(Mat binary)
    {
        byte[] data = PixelLoop.GetData<byte>(binary);
        int cols = binary.Cols;
        int rows = binary.Rows;
        int bright = 0;
        int dark = 0;

        for (int x = 0; x < cols; x++)
        {
            Count(data, x, 0, cols, ref bright, ref dark);
            Count(data, x, rows - 1, cols, ref bright, ref dark);
        }
        for (int y = 0; y < rows; y++)
        {
            Count(data, 0, y, cols, ref bright, ref dark);
            Count(data, cols - 1, y, cols, ref bright, ref dark);
        }

        return bright >= dark;
    }

    private static void Count(byte[] data, int x, int y, int cols, ref int bright, ref int dark)
    {
        if (data[y * cols + x] > 0)
        {
            bright++;
        }
        else
        {
            dark++;
        }
    }
}
