using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Batch;

public readonly record struct BatchProgress(int Completed, int Total, string CurrentFile, int Failed = 0, int Skipped = 0);

public interface IBatchProcessingService
{
    Task RunAsync(
        IReadOnlyList<string> inputFiles,
        IBackgroundRemovalStrategy strategy,
        StrategyContext context,
        string outputFolder,
        IProgress<BatchProgress>? progress,
        CancellationToken ct,
        BatchExportOptions? exportOptions = null);
}

/// <summary>
/// Applies one strategy/context to every file in a list, full resolution, exporting each as
/// "<name>_cutout.png" by default (or "_cutout.jpg" for JPEG output, "_cutout.webp" for WebP)
/// into the output folder. Files that fail to load/process are skipped (reported via progress's
/// CurrentFile) rather than aborting the whole batch.
/// </summary>
public sealed class BatchProcessingService : IBatchProcessingService
{
    private readonly IImageLoaderService _loader;
    private readonly IImageExportService _exporter;

    public BatchProcessingService(IImageLoaderService loader, IImageExportService exporter)
    {
        _loader = loader;
        _exporter = exporter;
    }

    public async Task RunAsync(
        IReadOnlyList<string> inputFiles,
        IBackgroundRemovalStrategy strategy,
        StrategyContext context,
        string outputFolder,
        IProgress<BatchProgress>? progress,
        CancellationToken ct,
        BatchExportOptions? exportOptions = null)
    {
        Directory.CreateDirectory(outputFolder);

        int failed = 0;
        int skipped = 0;
        for (int i = 0; i < inputFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string file = inputFiles[i];
            string baseName = Path.GetFileNameWithoutExtension(file);
            string suffix = exportOptions is { ExportJpeg: true } ? "_cutout.jpg" : exportOptions is { ExportWebp: true } ? "_cutout.webp" : "_cutout.png";
            string outPath = Path.Combine(outputFolder, baseName + suffix);

            // With SkipExisting, files whose cutout already exists are left untouched so a
            // batch can be re-run to fill in only the missing outputs (e.g. after adding
            // new files to the input folder).
            if (exportOptions is { SkipExisting: true } && File.Exists(outPath))
            {
                skipped++;
                progress?.Report(new BatchProgress(i + 1, inputFiles.Count, file, failed, skipped));
                continue;
            }

            progress?.Report(new BatchProgress(i, inputFiles.Count, file, failed, skipped));

            try
            {
                using var loaded = await _loader.LoadAsync(file, ct);
                using var result = await strategy.RunFullAsync(loaded.FullBgr, context, ct);

                if (exportOptions is { ExportJpeg: true })
                {
                    using var composited = CompositeForJpeg(result.Bgra, exportOptions, loaded.FullBgr);
                    await _exporter.ExportJpgAsync(composited, outPath, exportOptions.JpegQuality, ct);
                }
                else if (exportOptions is { ExportWebp: true })
                {
                    // WebP keeps the transparency like PNG but is typically much smaller.
                    await _exporter.ExportWebpAsync(result.Bgra, outPath, exportOptions.JpegQuality, ct);
                }
                else
                {
                    await _exporter.ExportPngAsync(result.Bgra, outPath, ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Skip files that fail to load/process; the batch continues with the rest.
                failed++;
            }
        }

        progress?.Report(new BatchProgress(inputFiles.Count, inputFiles.Count, string.Empty, failed, skipped));
    }

    /// <summary>Composites a transparent cutout onto the requested background for JPEG output.</summary>
    private static Mat CompositeForJpeg(Mat bgra, BatchExportOptions options, Mat sourceBgr)
    {
        switch (options.BackgroundMode)
        {
            case ExportBackgroundMode.Gradient:
                return BackgroundCompositingService.CompositeOntoGradient(
                    bgra,
                    new Vec3b(options.GradientTop.B, options.GradientTop.G, options.GradientTop.R),
                    new Vec3b(options.GradientBottom.B, options.GradientBottom.G, options.GradientBottom.R));
            case ExportBackgroundMode.Blur:
                return BackgroundCompositingService.CompositeOntoBlurredImage(bgra, sourceBgr, options.BlurRadius);
            default:
                return BackgroundCompositingService.CompositeOntoColor(
                    bgra,
                    new Vec3b(options.SolidColor.B, options.SolidColor.G, options.SolidColor.R));
        }
    }
}
