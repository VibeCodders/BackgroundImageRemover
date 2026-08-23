using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Contract for tool sessions whose preview accepts freehand strokes. The view forwards
/// <c>StrokeStart/Move/End</c> events (with the brush radius in display units) to the session,
/// which stamps/records the stroke in its own working data. Shared by every brush-based tool
/// (Blur, Sharpen, Mosaic, Heal, Retouch, Pen, Lasso, Clone Stamp) so their views inherit the
/// same stroke handlers from <see cref="Views.BrushStrokeSessionViewBase"/>.
/// </summary>
public interface IBrushStrokeSession
{
    /// <summary>Brush radius in display units (DIPs); the view converts it to pixel radius.</summary>
    double BrushRadius { get; }

    /// <summary>Called when a stroke starts (mouse-down on the preview).</summary>
    void OnStrokeStart(WpfPoint imagePoint, double pixelRadius);

    /// <summary>Called while a stroke is dragged across the preview.</summary>
    void OnStrokeMove(WpfPoint imagePoint, double pixelRadius);

    /// <summary>Called when a stroke ends (mouse-up on the preview).</summary>
    void OnStrokeEnd();
}
