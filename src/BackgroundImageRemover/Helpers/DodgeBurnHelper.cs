using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Helper class for dodge and burn operations.
/// Reduces duplication in DodgeBurnService by providing shared logic for channel adjustment.
/// </summary>
public static class DodgeBurnHelper
{
    /// <summary>
    /// Applies dodge or burn to a single channel.
    /// </summary>
    /// <param name="channel">Input channel Mat.</param>
    /// <param name="dodge">If true, applies dodge (brightening); if false, applies burn (darkening).</param>
    /// <param name="strength">Strength of the effect (0-1).</param>
    /// <returns>Adjusted channel Mat (caller owns disposal).</returns>
    public static Mat ApplyChannelAdjustment(Mat channel, bool dodge, double strength)
    {
        var adjusted = new Mat();
        if (dodge)
        {
            Cv2.AddWeighted(channel, 1.0 + strength, channel, strength, 0, adjusted);
        }
        else
        {
            Cv2.AddWeighted(channel, 1.0 - strength, channel, -strength, 0, adjusted);
        }
        
        // Clamp values to valid range
        Cv2.Min(adjusted, new Scalar(255), adjusted);
        Cv2.Max(adjusted, new Scalar(0), adjusted);
        
        return adjusted;
    }

    /// <summary>
    /// Applies dodge or burn to all channels of a BGR image.
    /// </summary>
    /// <param name="bgr">Input BGR image.</param>
    /// <param name="dodge">If true, applies dodge (brightening); if false, applies burn (darkening).</param>
    /// <param name="strength">Strength of the effect (0-1).</param>
    /// <returns>Adjusted BGR image (caller owns disposal).</returns>
    public static Mat ApplyToAllChannels(Mat bgr, bool dodge, double strength)
    {
        var result = bgr.Clone();
        var channels = new Mat[3];
        Cv2.Split(result, out channels);
        
        try
        {
            for (int i = 0; i < 3; i++)
            {
                using var adjusted = ApplyChannelAdjustment(channels[i], dodge, strength);
                adjusted.CopyTo(channels[i]);
            }
            
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }
}
