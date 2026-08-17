using BackgroundImageRemover.Services.Onnx;

namespace BackgroundImageRemover.Tests.Services;

public class ModelDownloadProgressTests
{
    [Fact]
    public void FractionComplete_IsNull_WhenTotalBytesIsUnknown()
    {
        var progress = new ModelDownloadProgress(BytesReceived: 100, TotalBytes: null);

        Assert.Null(progress.FractionComplete);
    }

    [Fact]
    public void FractionComplete_IsNull_WhenTotalBytesIsZero()
    {
        var progress = new ModelDownloadProgress(BytesReceived: 0, TotalBytes: 0);

        Assert.Null(progress.FractionComplete);
    }

    [Fact]
    public void FractionComplete_ComputesTheRatio_WhenTotalBytesIsKnown()
    {
        var progress = new ModelDownloadProgress(BytesReceived: 25, TotalBytes: 100);

        Assert.Equal(0.25, progress.FractionComplete);
    }

    [Fact]
    public void FractionComplete_CanExceedOne_WhenBytesReceivedOvershootsTotal()
    {
        // No clamping in the record itself; overshoot (e.g. from a stale Content-Length) is
        // the caller's concern to clamp for display purposes.
        var progress = new ModelDownloadProgress(BytesReceived: 150, TotalBytes: 100);

        Assert.Equal(1.5, progress.FractionComplete);
    }
}
