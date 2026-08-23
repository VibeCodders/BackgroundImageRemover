namespace BackgroundImageRemover.Models;

/// <summary>Which mouse interaction an <c>ImagePreviewControl</c> should currently offer.</summary>
public enum InteractionMode
{
    None,
    DrawRect,
    EditRect,
    ScribbleForeground,
    ScribbleBackground,
    EraseForeground,
    EraseBackground,
    Brush,
    Lasso,
    MagicWand,
    SamClick
}
