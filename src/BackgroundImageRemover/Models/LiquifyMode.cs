namespace BackgroundImageRemover.Models;

/// <summary>The distortion applied by the Liquify tool around a center point.</summary>
public enum LiquifyMode
{
    Pinch,
    Bloat,
    Twirl,
    PushLeft,
    PushRight,
    PushUp,
    PushDown
}
