namespace BackgroundImageRemover.Models;

/// <summary>How a background image is fitted to the composited canvas in the Compose tool.</summary>
public enum BackgroundFitMode
{
    /// <summary>Stretch to fill the canvas, ignoring aspect ratio.</summary>
    Stretch,

    /// <summary>Scale to fully cover the canvas, cropping the overflow (aspect preserved).</summary>
    Cover,

    /// <summary>Scale to fit entirely inside the canvas, leaving transparent/empty margins (aspect preserved).</summary>
    Contain,

    /// <summary>Repeat the background image as a tile across the canvas.</summary>
    Tile
}
