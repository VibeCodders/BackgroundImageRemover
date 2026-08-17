namespace BackgroundImageRemover.Models;

/// <summary>
/// How far the Uncrop canvas extends beyond the original image on each side, in source-image
/// pixels. The single source of truth shared by the aspect-ratio presets, the drag handles, and
/// the numeric padding fields in the Uncrop window.
/// </summary>
public readonly record struct CanvasPadding(int Left, int Top, int Right, int Bottom)
{
    public static readonly CanvasPadding Zero = new(0, 0, 0, 0);

    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}
