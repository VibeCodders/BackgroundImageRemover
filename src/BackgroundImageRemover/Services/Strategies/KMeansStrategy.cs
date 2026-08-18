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

        var labelData = new int[total];
        labels.GetArray(out labelData);

        // Count how many pixels of each cluster sit on the image border.
        var borderCount = new int[clusterCount];
        int totalBorder = 0;
        for (int x = 0; x < cols; x++)
        {
            CountBorder(labelData, x, 0, cols, borderCount); ++totalBorder;
            CountBorder(labelData, x, rows - 1, cols, borderCount); ++totalBorder;
        }
        for (int y = 0; y < rows; y++)
        {
            CountBorder(labelData, 0, y, cols, borderCount); ++totalBorder;
            CountBorder(labelData, cols - 1, y, cols, borderCount); ++totalBorder;
        }

        var isBackground = new bool[clusterCount];
        for (int c = 0; c < clusterCount; c++)
        {
            isBackground[c] = borderCount[c] / (double)Math.Max(1, totalBorder) >= borderFraction;
        }

        var mask = new Mat(rows, cols, MatType.CV_8UC1);
        var maskData = new byte[total];
        for (int i = 0; i < total; i++)
        {
            maskData[i] = isBackground[labelData[i]] ? (byte)0 : (byte)255;
        }
        mask.SetArray(maskData);

        var feathered = new Mat();
        Cv2.GaussianBlur(mask, feathered, new Size(5, 5), 0);
        mask.Dispose();
        return feathered;
    }

    private static void CountBorder(int[] labels, int x, int y, int cols, int[] borderCount)
    {
        var label = labels[y * cols + x];
        if (label >= 0 && label < borderCount.Length)
        {
            borderCount[label]++;
        }
    }
}
