using BackgroundImageRemover.Helpers;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Tests.Helpers;

public class MaskBrushHelperTests
{
    [Fact]
    public void StampSegment_ConnectsDistantPoints_WithContinuousStroke()
    {
        using var mask = new Mat(60, 60, MatType.CV_8UC1, Scalar.All(0));

        // A single jump from corner to corner must paint the whole diagonal, not just dots
        // at the ends (the bug that left gaps on fast mouse movement).
        MaskBrushHelper.StampSegment(mask, new WpfPoint(5, 5), new WpfPoint(55, 55), pixelRadius: 4);

        int count = Cv2.CountNonZero(mask);
        // The diagonal is ~50px long and ~8px thick: expect a solid band, far more than two dots.
        Assert.True(count > 150, $"expected a continuous diagonal band, got only {count} pixels");
        Assert.True(mask.At<byte>(30, 30) > 0, "mid-stroke point was not painted");
    }

    [Fact]
    public void StampSegment_ZeroLength_PaintsDot()
    {
        using var mask = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));

        MaskBrushHelper.StampSegment(mask, new WpfPoint(20, 20), new WpfPoint(20, 20), pixelRadius: 5);

        Assert.True(Cv2.CountNonZero(mask) > 0);
        Assert.True(mask.At<byte>(20, 20) > 0);
    }

    [Fact]
    public void StampSegment_TwoJumps_PaintContinuousHorizontalStroke()
    {
        using var mask = new Mat(40, 100, MatType.CV_8UC1, Scalar.All(0));

        // Fast movement: the mouse jumps from 10 -> 40 -> 90 without intermediate events.
        MaskBrushHelper.StampSegment(mask, new WpfPoint(10, 20), new WpfPoint(40, 20), pixelRadius: 3);
        MaskBrushHelper.StampSegment(mask, new WpfPoint(40, 20), new WpfPoint(90, 20), pixelRadius: 3);

        // Every column between 10 and 90 must have been painted (no gaps).
        for (int x = 10; x <= 90; x++)
        {
            Assert.True(mask.At<byte>(20, x) > 0, $"gap in stroke at column {x}");
        }
    }
}
