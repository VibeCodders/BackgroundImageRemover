namespace BackgroundImageRemover.Models;

/// <summary>Which censorship effect the Mosaic tool applies to the selected region.</summary>
public enum MosaicMode
{
    Pixelate,
    Blur,
    Median,
    SolidFill,
    Crystallize
}
