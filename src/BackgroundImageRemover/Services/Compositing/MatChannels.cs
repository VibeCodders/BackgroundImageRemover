using OpenCvSharp;

namespace BackgroundImageRemover.Services.Compositing;

/// <summary>
/// Disposable wrapper around <see cref="Cv2.Split"/>'s per-channel Mats, so split/mutate/merge
/// call sites don't each hand-roll their own try/finally dispose loop.
/// </summary>
public readonly struct ChannelSplit : IDisposable
{
    public Mat[] Channels { get; }

    private ChannelSplit(Mat[] channels) => Channels = channels;

    public static ChannelSplit Of(Mat mat) => new(Cv2.Split(mat));

    public Mat this[int index] => Channels[index];

    public void Dispose()
    {
        foreach (var c in Channels)
        {
            c.Dispose();
        }
    }
}
