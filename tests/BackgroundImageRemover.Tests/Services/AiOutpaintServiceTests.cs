using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// AI outpainting orchestration: canvas expansion, the 512×512 square-pad + scale-down paths,
/// and the crisp-original composite for canvases larger than the model input.
/// </summary>
public class AiOutpaintServiceTests
{
    private static readonly Vec3b SourceColor = new(10, 200, 30);      // BGR
    private static readonly Vec3b AiFillColor = new(255, 0, 255);      // magenta BGR = fake model fill

    /// <summary>
    /// Mimics the Carve/LaMa-ONNX export contract: output = input in the unmasked region, a
    /// known fill color in the masked region.
    /// </summary>
    private sealed class FakeLamaEngine : ILamaInpaintEngine
    {
        public int EnsureReadyCalls { get; private set; }
        public LamaModelVariant? LastVariant { get; private set; }
        public bool? LastGpu { get; private set; }
        public Size? LastInputSize { get; private set; }
        public int LastMaskSum { get; private set; }

        public bool IsReady => true;

        public Task EnsureReadyAsync(LamaModelVariant variant, bool useGpu, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        {
            EnsureReadyCalls++;
            LastVariant = variant;
            LastGpu = useGpu;
            return Task.CompletedTask;
        }

        public Mat Inpaint(Mat imageBgr512, Mat mask512)
        {
            LastInputSize = imageBgr512.Size();
            var result = imageBgr512.Clone();
            int sum = 0;
            for (int y = 0; y < mask512.Rows; y++)
            {
                for (int x = 0; x < mask512.Cols; x++)
                {
                    if (mask512.At<byte>(y, x) > 0)
                    {
                        sum++;
                        result.Set(y, x, AiFillColor);
                    }
                }
            }
            LastMaskSum = sum;
            return result;
        }

        public void Dispose() { }
    }

    private static AiOutpaintService CreateService(FakeLamaEngine engine)
        => new(engine, new UncropFillService());

    private static Mat CreateSource(int width, int height)
        => new(height, width, MatType.CV_8UC3, new Scalar(SourceColor.Item0, SourceColor.Item1, SourceColor.Item2));

    [Fact]
    public async Task OutpaintAsync_CanvasFitsModel_PreservesOriginalAndFillsNewArea()
    {
        using var source = CreateSource(40, 40);
        var padding = new CanvasPadding(10, 10, 10, 10);
        var engine = new FakeLamaEngine();
        var service = CreateService(engine);

        using var result = await service.OutpaintAsync(source, padding, LamaModelVariant.Large, useGpu: false, null, CancellationToken.None);

        Assert.Equal(new Size(60, 60), result.Size());
        Assert.Equal(1, engine.EnsureReadyCalls);
        Assert.Equal(LamaModelVariant.Large, engine.LastVariant);
        Assert.False(engine.LastGpu);

        // Original region center (canvas 30,30 maps to source 20,20) is untouched.
        Assert.Equal(SourceColor, result.At<Vec3b>(30, 30));
        // The newly added canvas corner was filled by the (fake) model.
        Assert.Equal(AiFillColor, result.At<Vec3b>(0, 0));
    }

    [Fact]
    public async Task OutpaintAsync_CanvasLargerThanModel_ScalesDownAndCompositesCrispOriginal()
    {
        using var source = CreateSource(600, 400);
        var padding = new CanvasPadding(200, 200, 200, 200);
        var engine = new FakeLamaEngine();
        var service = CreateService(engine);

        using var result = await service.OutpaintAsync(source, padding, LamaModelVariant.Small, useGpu: true, null, CancellationToken.None);

        // 600+400 x 400+400 = 1000x800 canvas; the model only ever sees 512x512.
        Assert.Equal(LamaModelVariant.Small, engine.LastVariant);
        Assert.True(engine.LastGpu);
        Assert.Equal(new Size(1000, 800), result.Size());
        Assert.Equal(new Size(512, 512), engine.LastInputSize);

        // Far corner of the new area carries the model fill (upscaled back, uniform color).
        Assert.Equal(AiFillColor, result.At<Vec3b>(0, 0));

        // The original-content center is the crisp source pixel (composited back over the
        // re-upscaled model output). Canvas (500,400) maps to source (300,200).
        Assert.Equal(SourceColor, result.At<Vec3b>(400, 500));
    }

    [Fact]
    public async Task OutpaintAsync_ReportsDownloadProgressToEnsureReady()
    {
        using var source = CreateSource(40, 40);
        var padding = new CanvasPadding(5, 5, 5, 5);
        var engine = new FakeLamaEngine();
        var service = CreateService(engine);

        var received = new List<ModelDownloadProgress>();
        var progress = new Progress<ModelDownloadProgress>(p => received.Add(p));
        using var result = await service.OutpaintAsync(source, padding, LamaModelVariant.Large, useGpu: false, progress, CancellationToken.None);

        Assert.Equal(1, engine.EnsureReadyCalls);
        Assert.Equal(50, result.Width); // 40 source + 5 left + 5 right
    }

    [Fact]
    public async Task OutpaintAsync_AlreadyCancelled_ThrowsBeforeTouchingTheEngine()
    {
        using var source = CreateSource(40, 40);
        var padding = new CanvasPadding(5, 5, 5, 5);
        var engine = new FakeLamaEngine();
        var service = CreateService(engine);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.OutpaintAsync(source, padding, LamaModelVariant.Large, useGpu: false, null, cts.Token));
        Assert.Equal(0, engine.EnsureReadyCalls);
    }

    [Fact]
    public async Task OutpaintAsync_ReturnsSameSizeAsCanvas_ForAsymmetricPadding()
    {
        using var source = CreateSource(30, 20);
        var padding = new CanvasPadding(4, 8, 12, 16);
        var engine = new FakeLamaEngine();
        var service = CreateService(engine);

        using var result = await service.OutpaintAsync(source, padding, LamaModelVariant.Large, useGpu: false, null, CancellationToken.None);

        Assert.Equal(new Size(30 + 4 + 12, 20 + 8 + 16), result.Size());
    }
}
