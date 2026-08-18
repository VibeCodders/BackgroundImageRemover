using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Helper class for channel split operations used in color decontamination.
/// </summary>
internal static class ChannelSplit
{
    /// <summary>
    /// Splits a multi-channel Mat into individual channel Mats.
    /// </summary>
    public static SplitResult Of(Mat mat)
    {
        var channels = Cv2.Split(mat);
        return new SplitResult(channels);
    }

    /// <summary>
    /// Holds the result of a channel split operation with proper disposal.
    /// </summary>
    public class SplitResult : IDisposable
    {
        public Mat[] Channels { get; }

        public SplitResult(Mat[] channels)
        {
            Channels = channels;
        }

        public void Dispose()
        {
            foreach (var channel in Channels)
            {
                channel.Dispose();
            }
        }
    }
}