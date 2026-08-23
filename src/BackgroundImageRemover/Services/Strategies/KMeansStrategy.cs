using System.Threading.Tasks;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes the background by clustering the image's colors with k-means and discarding every
/// cluster that touches the image border (the background is assumed to be the color or colors
/// that surround the subject). Works well on flat or gently-graded backgrounds, including
/// multi-color studio backdrops.
/// </summary>
public sealed class KMeansStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.KMeans;

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        int clusterCount = Math.Clamp(context.KMeansClusters, 2, 16);
        double borderFraction = Math.Clamp(context.KMeansBorderFraction, 0.05, 0.95);

        int rows = bgr.Rows;
        int cols = bgr.Cols;
        int total = rows * cols;

        using var reshaped = bgr.Reshape(1, total);
        using var data = new Mat();
        reshaped.ConvertTo(data, MatType.CV_32F);

        using var labels = new Mat();
        using var centers = new Mat();
        Cv2.Kmeans(data, clusterCount, labels,
            new TermCriteria(CriteriaTypes.MaxIter | CriteriaTypes.Eps, 10, 1.0),
            attempts: 3, KMeansFlags.RandomCenters, centers);

        ct.ThrowIfCancellationRequested();

        // Count how many pixels of each cluster sit on the image border. The border is only
        // O(rows + cols) pixels, so reading the labels Mat directly is cheap and avoids a
        // full-image GetArray copy.
        var borderCount = new int[clusterCount];
        int totalBorder = 0;
        for (int x = 0; x < cols; x++)
        {
            CountBorder(labels, x, 0, borderCount); ++totalBorder;
            CountBorder(labels, x, rows - 1, borderCount); ++totalBorder;
        }
        for (int y = 0; y < rows; y++)
        {
            CountBorder(labels, 0, y, borderCount); ++totalBorder;
            CountBorder(labels, cols - 1, y, borderCount); ++totalBorder;
        }

        var isBackground = new bool[clusterCount];
        for (int c = 0; c < clusterCount; c++)
        {
            isBackground[c] = borderCount[c] / (double)Math.Max(1, totalBorder) >= borderFraction;
        }

        // Label→mask is a per-pixel map, so it is written straight into the native buffer in
        // parallel — no GetArray/SetArray copies. Kmeans returns the labels as a flat N×1
        // vector, so they are indexed linearly (image pixel i = y*cols + x). Identical math to
        // the sequential version.
        var mask = new Mat(rows, cols, MatType.CV_8UC1);
        unsafe
        {
            int* labelPtr = (int*)labels.DataPointer;
            byte* maskPtr = (byte*)mask.DataPointer;
            long maskStep = mask.Step();
            Parallel.For(0, rows, y =>
            {
                byte* maskRow = maskPtr + y * maskStep;
                int rowBase = y * cols;
                for (int x = 0; x < cols; x++)
                {
                    maskRow[x] = isBackground[labelPtr[rowBase + x]] ? (byte)0 : (byte)255;
                }
            });
        }

        return MaskHelpers.Feather(mask);
    }

    private static void CountBorder(Mat labels, int x, int y, int[] borderCount)
    {
        var label = labels.At<int>(y, x);
        if (label >= 0 && label < borderCount.Length)
        {
            borderCount[label]++;
        }
    }
}
