using BackgroundImageRemover.Helpers;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Tests.Helpers;

public class BrushStrokeControllerTests
{
    [Fact]
    public void Begin_StampsInitialDot_And_RecordsAnchor()
    {
        var controller = new BrushStrokeController();
        var segments = new List<(WpfPoint From, WpfPoint To, double Radius)>();

        controller.Begin(new WpfPoint(10, 10), 5, (from, to, r) => segments.Add((from, to, r)));

        Assert.Single(segments);
        Assert.Equal(new WpfPoint(10, 10), segments[0].From);
        Assert.Equal(new WpfPoint(10, 10), segments[0].To);
        Assert.Equal(5, segments[0].Radius);
    }

    [Fact]
    public void Extend_StampsFromPreviousAnchor_And_UpdatesAnchor()
    {
        var controller = new BrushStrokeController();
        var segments = new List<(WpfPoint From, WpfPoint To, double Radius)>();

        controller.Begin(new WpfPoint(10, 10), 5, (from, to, r) => segments.Add((from, to, r)));
        controller.Extend(new WpfPoint(30, 10), 5, (from, to, r) => segments.Add((from, to, r)));

        Assert.Equal(2, segments.Count);
        Assert.Equal(new WpfPoint(10, 10), segments[1].From);
        Assert.Equal(new WpfPoint(30, 10), segments[1].To);
    }

    [Fact]
    public void End_ClearsAnchor_So_NextStrokeStartsFresh()
    {
        var controller = new BrushStrokeController();
        var segments = new List<(WpfPoint From, WpfPoint To, double Radius)>();

        controller.Begin(new WpfPoint(10, 10), 5, (from, to, r) => segments.Add((from, to, r)));
        controller.End();
        controller.Begin(new WpfPoint(50, 50), 5, (from, to, r) => segments.Add((from, to, r)));

        // The second Begin must not be connected back to the first stroke's last point.
        Assert.Equal(new WpfPoint(50, 50), segments[1].From);
    }
}
