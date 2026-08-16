using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class BatchProcessingServiceTests
{
    private sealed class FakeImageLoaderService : IImageLoaderService
    {
        public string? PathToFail;

        public Task<LoadedImage> LoadAsync(string filePath, CancellationToken ct = default)
        {
            if (filePath == PathToFail)
            {
                throw new InvalidOperationException("simulated decode failure");
            }
            return Task.FromResult(new LoadedImage(filePath, new Mat(4, 4, MatType.CV_8UC3, Scalar.All(128))));
        }
    }

    private sealed class FakeImageExportService : IImageExportService
    {
        public readonly List<string> ExportedPaths = new();

        public Task ExportPngAsync(Mat bgra, string filePath, CancellationToken ct = default)
        {
            ExportedPaths.Add(filePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStrategy : IBackgroundRemovalStrategy
    {
        public StrategyKind Kind => StrategyKind.ChromaKey;

        public Task<RemovalResult> RunPreviewAsync(Mat previewBgr, StrategyContext context, CancellationToken ct)
            => RunAsync(previewBgr);

        public Task<RemovalResult> RunFullAsync(Mat fullBgr, StrategyContext context, CancellationToken ct)
            => RunAsync(fullBgr);

        private static Task<RemovalResult> RunAsync(Mat bgr)
        {
            var bgra = new Mat();
            Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
            return Task.FromResult(new RemovalResult(bgra, 0));
        }
    }

    [Fact]
    public async Task RunAsync_ExportsOneFilePerInput()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);

        var files = new[] { "a.png", "b.png", "c.png" };
        await service.RunAsync(files, new FakeStrategy(), new StrategyContext(), "out", progress: null, CancellationToken.None);

        Assert.Equal(3, exporter.ExportedPaths.Count);
    }

    [Fact]
    public async Task RunAsync_SkipsFilesThatFailToLoad_AndContinuesWithTheRest()
    {
        var loader = new FakeImageLoaderService { PathToFail = "bad.png" };
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);

        var files = new[] { "a.png", "bad.png", "c.png" };
        await service.RunAsync(files, new FakeStrategy(), new StrategyContext(), "out", progress: null, CancellationToken.None);

        Assert.Equal(2, exporter.ExportedPaths.Count);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressForEveryFile()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var reported = new List<BatchProgress>();

        var files = new[] { "a.png", "b.png" };
        await service.RunAsync(files, new FakeStrategy(), new StrategyContext(), "out",
            new Progress<BatchProgress>(p => reported.Add(p)), CancellationToken.None);

        // Progress<T> callbacks are marshalled asynchronously; give them a beat to arrive.
        await Task.Delay(50);

        Assert.Contains(reported, p => p.Completed == 0);
        Assert.Contains(reported, p => p.Completed == files.Length);
    }
}
