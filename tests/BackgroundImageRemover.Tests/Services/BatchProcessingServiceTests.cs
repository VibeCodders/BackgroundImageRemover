using System.IO;
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

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(4, 4, MatType.CV_8UC3, Scalar.All(128))));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(4, 4, MatType.CV_8UC3, Scalar.All(128))));
    }

    private sealed class FakeImageExportService : IImageExportService
    {
        public readonly List<string> ExportedPaths = new();
        public readonly List<int> JpegQualities = new();

        public Task ExportPngAsync(Mat bgra, string filePath, CancellationToken ct = default)
        {
            ExportedPaths.Add(filePath);
            return Task.CompletedTask;
        }

        public Task ExportJpgAsync(Mat bgr, string filePath, int quality = 95, CancellationToken ct = default)
        {
            ExportedPaths.Add(filePath);
            JpegQualities.Add(quality);
            return Task.CompletedTask;
        }

        public Task ExportWebpAsync(Mat bgra, string filePath, int quality = 90, CancellationToken ct = default)
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
    public async Task RunAsync_WithJpegOptions_ExportsJpgFilesWithQuality()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);

        var options = new BatchExportOptions
        {
            ExportJpeg = true,
            JpegQuality = 80,
            BackgroundMode = ExportBackgroundMode.SolidColor,
            SolidColor = System.Windows.Media.Colors.White
        };

        await service.RunAsync(
            new[] { "a.png", "b.png" }, new FakeStrategy(), new StrategyContext(), "out",
            progress: null, CancellationToken.None, options);

        Assert.Equal(2, exporter.ExportedPaths.Count);
        Assert.All(exporter.ExportedPaths, p => Assert.EndsWith("_cutout.jpg", p, StringComparison.OrdinalIgnoreCase));
        Assert.All(exporter.JpegQualities, q => Assert.Equal(80, q));
    }

    [Fact]
    public async Task RunAsync_DefaultOptions_ExportsPngFiles()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);

        await service.RunAsync(
            new[] { "a.png" }, new FakeStrategy(), new StrategyContext(), "out",
            progress: null, CancellationToken.None);

        Assert.Single(exporter.ExportedPaths);
        Assert.EndsWith("_cutout.png", exporter.ExportedPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WithWebpOptions_ExportsWebpFiles()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var options = new BatchExportOptions { ExportWebp = true };

        await service.RunAsync(
            new[] { "a.png", "b.png" }, new FakeStrategy(), new StrategyContext(), "out",
            progress: null, CancellationToken.None, options);

        Assert.Equal(2, exporter.ExportedPaths.Count);
        Assert.All(exporter.ExportedPaths, p => Assert.EndsWith("_cutout.webp", p, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_WithWebpAndSkipExisting_ChecksWebpExtension()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var outputDir = Path.Combine(Path.GetTempPath(), $"batch_webp_skip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            // Only the WebP output counts as "existing" for a WebP batch: a stale PNG from a
            // previous run must not suppress the WebP export.
            File.WriteAllText(Path.Combine(outputDir, "a_cutout.png"), "old png");
            File.WriteAllText(Path.Combine(outputDir, "b_cutout.webp"), "existing webp");
            var reported = new List<BatchProgress>();

            await service.RunAsync(
                new[] { "a.png", "b.png" }, new FakeStrategy(), new StrategyContext(), outputDir,
                new CollectingProgress(reported), CancellationToken.None, new BatchExportOptions { ExportWebp = true, SkipExisting = true });

            Assert.Single(exporter.ExportedPaths); // a.webp written, b.webp skipped
            Assert.EndsWith("_cutout.webp", exporter.ExportedPaths[0], StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, reported.Last().Skipped);
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_WithSkipExisting_SkipsFilesWhoseOutputAlreadyExists()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var outputDir = Path.Combine(Path.GetTempPath(), $"batch_skip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            // b.png already has a cutout in the output folder; a.png and c.png do not.
            File.WriteAllText(Path.Combine(outputDir, "b_cutout.png"), "existing");
            var options = new BatchExportOptions { ExportJpeg = false, SkipExisting = true };

            await service.RunAsync(
                new[] { "a.png", "b.png", "c.png" }, new FakeStrategy(), new StrategyContext(), outputDir,
                progress: null, CancellationToken.None, options);

            Assert.Equal(2, exporter.ExportedPaths.Count);
            Assert.DoesNotContain(exporter.ExportedPaths, p => p.EndsWith("b_cutout.png", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_WithSkipExisting_ReportsSkippedCountInFinalProgress()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var outputDir = Path.Combine(Path.GetTempPath(), $"batch_skip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            File.WriteAllText(Path.Combine(outputDir, "a_cutout.png"), "existing");
            File.WriteAllText(Path.Combine(outputDir, "b_cutout.png"), "existing");
            var reported = new List<BatchProgress>();

            await service.RunAsync(
                new[] { "a.png", "b.png", "c.png" }, new FakeStrategy(), new StrategyContext(), outputDir,
                new CollectingProgress(reported), CancellationToken.None, new BatchExportOptions { SkipExisting = true });

            var final = reported.Last();
            Assert.Equal(2, final.Skipped);
            Assert.Equal(0, final.Failed);
            Assert.Single(exporter.ExportedPaths); // only c.png was processed
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_WithoutSkipExisting_ReExportsFilesThatAlreadyHaveOutput()
    {
        var loader = new FakeImageLoaderService();
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var outputDir = Path.Combine(Path.GetTempPath(), $"batch_skip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            File.WriteAllText(Path.Combine(outputDir, "a_cutout.png"), "existing");

            await service.RunAsync(
                new[] { "a.png" }, new FakeStrategy(), new StrategyContext(), outputDir,
                progress: null, CancellationToken.None);

            Assert.Single(exporter.ExportedPaths); // overwritten even though the output exists
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_ReportsFailedCountInFinalProgress()
    {
        var loader = new FakeImageLoaderService { PathToFail = "bad.png" };
        var exporter = new FakeImageExportService();
        var service = new BatchProcessingService(loader, exporter);
        var reported = new List<BatchProgress>();

        var files = new[] { "ok1.png", "bad.png", "ok2.png" };
        await service.RunAsync(files, new FakeStrategy(), new StrategyContext(), "out",
            new CollectingProgress(reported), CancellationToken.None);

        var final = reported.Last();
        Assert.Equal(3, final.Total);
        Assert.Equal(1, final.Failed);
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
            new CollectingProgress(reported), CancellationToken.None);

        // The service reports synchronously inside its loop, so a plain IProgress collects
        // every event deterministically (Progress<T> marshals asynchronously and was flaky
        // under parallel test load).
        Assert.Contains(reported, p => p.Completed == 0);
        Assert.Contains(reported, p => p.Completed == files.Length);
    }

    private sealed class CollectingProgress : IProgress<BatchProgress>
    {
        private readonly List<BatchProgress> _target;

        public CollectingProgress(List<BatchProgress> target) => _target = target;

        public void Report(BatchProgress value) => _target.Add(value);
    }
}
