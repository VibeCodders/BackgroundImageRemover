namespace BackgroundImageRemover.Models;

/// <summary>How the Resize tool interprets its numeric inputs.</summary>
public enum ResizeMode
{
    ExactSize,
    Percent,
    FitWithin,
    FillTo,
    LongestSide,
    Megapixels
}
