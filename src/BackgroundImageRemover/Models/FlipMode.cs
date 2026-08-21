namespace BackgroundImageRemover.Models;

/// <summary>
/// Direction in which an image is flipped.
/// </summary>
public enum ImageFlipMode
{
    /// <summary>Mirror around the y-axis (left/right swap).</summary>
    Horizontal,

    /// <summary>Mirror around the x-axis (top/bottom swap).</summary>
    Vertical,

    /// <summary>Mirror around both axes (equivalent to a 180° rotation).</summary>
    Both
}
