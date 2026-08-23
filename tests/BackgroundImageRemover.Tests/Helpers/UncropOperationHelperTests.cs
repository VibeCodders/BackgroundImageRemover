using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public class UncropOperationHelperTests
{
    private sealed class FakeAiOutpaintService : IAiOutpaintService
    {
        public int Calls { get; private set; }
        public CanvasPadding? LastPadding { get; private set; }
        public LamaModelVariant LastVariant { get; private set; }
        public bool LastGpu { get; private set; }

        public Task<Mat> OutpaintAsync(Mat sourceBgr, CanvasPadding padding, LamaModelVariant model, bool useGpu, IProgress<ModelDownloadProgress>? downloadProgress, CancellationToken ct)
        {
            Calls++;
            LastPadding = padding;
            LastVariant = model;
            LastGpu = useGpu;
            return Task.FromResult(new Mat(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(42)));
        }
    }

    [Fact]
    public async Task ExecuteUncropAsync_AiOutpaint_RoutesToTheAiService()
    {
        using var source = new Mat(20, 30, MatType.CV_8UC3, Scalar.All(7));
        var padding = new CanvasPadding(5, 5, 5, 5);
        var ai = new FakeAiOutpaintService();
        var config = new UncropOperationHelper.UncropConfig { Padding = padding, FillMode = UncropFillMode.AiOutpaint };

        using var result = await UncropOperationHelper.ExecuteUncropAsync(source, config, new FakeUncropFillService(), ai, null, CancellationToken.None);

        Assert.Equal(1, ai.Calls);
        Assert.Equal(padding, ai.LastPadding);
        Assert.Equal(new Size(40, 30), result.Size());
    }

    [Fact]
    public async Task ExecuteUncropAsync_AiOutpaint_ForwardsModelVariantAndGpuPreference()
    {
        using var source = new Mat(20, 30, MatType.CV_8UC3, Scalar.All(7));
        var padding = new CanvasPadding(5, 5, 5, 5);
        var ai = new FakeAiOutpaintService();
        var config = new UncropOperationHelper.UncropConfig
        {
            Padding = padding,
            FillMode = UncropFillMode.AiOutpaint,
            AiModelVariant = LamaModelVariant.Small,
            UseGpu = true
        };

        using var result = await UncropOperationHelper.ExecuteUncropAsync(source, config, new FakeUncropFillService(), ai, null, CancellationToken.None);

        Assert.Equal(1, ai.Calls);
        Assert.Equal(LamaModelVariant.Small, ai.LastVariant);
        Assert.True(ai.LastGpu);
        Assert.Equal(new Size(40, 30), result.Size());
    }

    [Fact]
    public async Task ExecuteUncropAsync_AiOutpaint_WithoutService_Throws()
    {
        using var source = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(7));
        var padding = new CanvasPadding(5, 5, 5, 5);
        var config = new UncropOperationHelper.UncropConfig { Padding = padding, FillMode = UncropFillMode.AiOutpaint };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UncropOperationHelper.ExecuteUncropAsync(source, config, new FakeUncropFillService(), cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteUncropAsync_NonAiMode_IgnoresTheAiService()
    {
        using var source = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(7));
        var padding = new CanvasPadding(5, 5, 5, 5);
        var ai = new FakeAiOutpaintService();
        var config = new UncropOperationHelper.UncropConfig { Padding = padding, FillMode = UncropFillMode.Mirror };

        using var result = await UncropOperationHelper.ExecuteUncropAsync(source, config, new FakeUncropFillService(), ai, null, CancellationToken.None);

        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData(UncropFillMode.AiOutpaint)]
    [InlineData(UncropFillMode.Mirror)]
    [InlineData(UncropFillMode.Inpaint)]
    public void CanExecute_Enabled_WhenPaddingIsSet(UncropFillMode fillMode)
    {
        var config = new UncropOperationHelper.UncropConfig { Padding = new CanvasPadding(10, 10, 10, 10), FillMode = fillMode };

        Assert.True(UncropOperationHelper.CanExecute(config));
    }

    [Theory]
    [InlineData(UncropFillMode.AiOutpaint)]
    [InlineData(UncropFillMode.Mirror)]
    public void CanExecute_Disabled_WhenPaddingIsZero(UncropFillMode fillMode)
    {
        var config = new UncropOperationHelper.UncropConfig { Padding = CanvasPadding.Zero, FillMode = fillMode };

        Assert.False(UncropOperationHelper.CanExecute(config));
    }
}
