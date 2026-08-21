using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Encapsulates the last-point tracking for a freehand brush stroke, connecting
/// consecutive points so fast strokes paint continuously. The caller supplies a
/// <paramref name="stamp"/> callback that paints each segment into its own mask,
/// keeping the controller free of any specific tool state.
/// </summary>
public sealed class BrushStrokeController
{
    private WpfPoint? _last;

    /// <summary>Starts a stroke at <paramref name="point"/>, stamping an initial dot, then
    /// records <paramref name="point"/> as the anchor for the next segment.</summary>
    public void Begin(WpfPoint point, double radius, Action<WpfPoint, WpfPoint, double> stamp)
    {
        _last = point;
        stamp(point, point, radius);
    }

    /// <summary>Extends the stroke to <paramref name="point"/>, stamping the segment from the
    /// previously recorded point (when one exists), then records <paramref name="point"/>.</summary>
    public void Extend(WpfPoint point, double radius, Action<WpfPoint, WpfPoint, double> stamp)
    {
        if (_last is { } last)
        {
            stamp(last, point, radius);
        }

        _last = point;
    }

    /// <summary>Ends the stroke, clearing the recorded anchor point.</summary>
    public void End() => _last = null;
}
