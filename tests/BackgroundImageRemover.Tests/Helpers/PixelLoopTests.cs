using BackgroundImageRemover.Helpers;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public class PixelLoopTests
{
    [Fact]
    public void ForEach_VisitsEveryPixelExactlyOnce()
    {
        var visited = new HashSet<(int Y, int X)>();
        int calls = 0;

        PixelLoop.ForEach(rows: 3, cols: 4, (y, x) =>
        {
            visited.Add((y, x));
            calls++;
        });

        Assert.Equal(12, calls);
        Assert.Equal(12, visited.Count);
        Assert.Contains((0, 0), visited);
        Assert.Contains((2, 3), visited);
    }

    [Fact]
    public void ForEach_Mat_UsesMatDimensions()
    {
        using var mat = new Mat(5, 7, MatType.CV_8UC1);
        var visited = new HashSet<(int Y, int X)>();

        PixelLoop.ForEach(mat, (y, x) => visited.Add((y, x)));

        Assert.Equal(35, visited.Count);
        Assert.Contains((4, 6), visited);
    }

    [Fact]
    public void ForEach_RowMajorOrder_VisitsAllColumnsBeforeNextRow()
    {
        var order = new List<(int Y, int X)>();

        PixelLoop.ForEach(rows: 2, cols: 3, (y, x) => order.Add((y, x)));

        Assert.Equal(new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) }, order);
    }

    [Fact]
    public void ForEach_ZeroSizedGrid_DoesNotInvokeCallback()
    {
        int calls = 0;

        PixelLoop.ForEach(rows: 0, cols: 5, (_, _) => calls++);
        PixelLoop.ForEach(rows: 5, cols: 0, (_, _) => calls++);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void ForEach_NullMat_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PixelLoop.ForEach((Mat)null!, (_, _) => { }));
    }

    [Fact]
    public void FillFloatParallel_MatchesSequentialFill()
    {
        const int w = 7;
        const int h = 9;
        using var mat = new Mat(h, w, MatType.CV_32FC1, Scalar.All(0));

        PixelLoop.FillFloatParallel(mat, (x, y) => x + y * w);

        // The parallel fill must produce the same values as a plain sequential assignment.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Assert.Equal((float)(x + y * w), mat.Get<float>(y, x));
            }
        }
    }

    [Fact]
    public void FillFloatParallel_Roi_OnlyWritesInsideRoi()
    {
        const int w = 12;
        const int h = 10;
        using var parent = new Mat(h, w, MatType.CV_32FC1, Scalar.All(7.0));
        var roi = new Rect(2, 3, 5, 4);
        using var roiView = new Mat(parent, roi);

        PixelLoop.FillFloatParallel(roiView, (x, y) => x * 100.0f + y);

        // Inside the ROI the values were written through the non-continuous view...
        for (int y = 0; y < roi.Height; y++)
        {
            for (int x = 0; x < roi.Width; x++)
            {
                Assert.Equal(x * 100.0f + y, parent.Get<float>(roi.Y + y, roi.X + x));
            }
        }

        // ...and outside the ROI the parent is untouched.
        Assert.Equal(7.0f, parent.Get<float>(0, 0));
        Assert.Equal(7.0f, parent.Get<float>(1, 1));
        Assert.Equal(7.0f, parent.Get<float>(5, 0));
        Assert.Equal(7.0f, parent.Get<float>(3, 10));
        Assert.Equal(7.0f, parent.Get<float>(9, 11));
    }

    [Fact]
    public void FillFloatParallel_EmptyOrZeroSizedMat_DoesNotThrow()
    {
        using var empty = new Mat();
        PixelLoop.FillFloatParallel(empty, (_, _) => 1f);

        using var zeroRows = new Mat(0, 5, MatType.CV_32FC1);
        PixelLoop.FillFloatParallel(zeroRows, (_, _) => 1f);
    }

    [Fact]
    public void FillFloatParallel_NullArguments_Throw()
    {
        using var mat = new Mat(3, 3, MatType.CV_32FC1);
        Assert.Throws<ArgumentNullException>(() => PixelLoop.FillFloatParallel(null!, (_, _) => 1f));
        Assert.Throws<ArgumentNullException>(() => PixelLoop.FillFloatParallel(mat, null!));
    }
}
