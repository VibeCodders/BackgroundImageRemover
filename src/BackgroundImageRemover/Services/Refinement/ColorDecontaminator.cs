using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Removes the original background color's cast ("spill") from the semi-transparent edge
/// pixels of a cutout. Strategy masks are feathered, so edge pixels keep the source image's
/// RGB even where the alpha is partial; without this step the old background shows up as a
/// colored halo once the cutout is composited over a new background.
/// </summary>
public static class ColorDecontaminator
{
    /// <summary>Default neighborhood radius (px) over which the background color is estimated.</summary>
    public const int DefaultEstimateRadius = 15; // matches the original fixed 31x31 kernel

    /// <summary>
    /// Decontaminates <paramref name="bgra"/> in place. When <paramref name="knownBackground"/> is
    /// supplied (chroma key), the background color is known exactly and the key's alpha is a soft
    /// key rather than true coverage, so a full unspill is unreliable; instead the dominant
    /// background channel is neutralized (classic spill suppression). Otherwise the background
    /// color is estimated per pixel from the surrounding fully-transparent pixels (within
    /// <paramref name="estimateRadius"/> pixels) and the pure foreground color is recovered as
    /// F = (C - (1-a)*B) / a.
    /// </summary>
    public static void Decontaminate(Mat bgra, Vec3b? knownBackground, int estimateRadius = DefaultEstimateRadius)
    {
        if (bgra.Channels() != 4)
        {
            return;
        }

        using var split = ChannelSplit.Of(bgra);
        if (knownBackground is { } kb)
        {
            ChromaKeyDespill.Despill(split.Channels, kb);
        }
        else
        {
            MatteUnspill.Unspill(split.Channels, estimateRadius);
        }

        Cv2.Merge(split.Channels, bgra);
    }
}