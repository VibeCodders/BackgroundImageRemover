namespace BackgroundImageRemover.Models;

/// <summary>Which mouse interaction an <c>ImagePreviewControl</c> should currently offer.</summary>
public enum InteractionMode
{
    None,
    DrawRect,
    ScribbleForeground,
    ScribbleBackground,
    Brush,
    MagicWand,
    SamClick
}
