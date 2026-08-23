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
}
