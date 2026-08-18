namespace BackgroundImageRemover.Models;

/// <summary>How an overlay's pixels are combined with the underlying image.</summary>
public enum OverlayBlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay
}
