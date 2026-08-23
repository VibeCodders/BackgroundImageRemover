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

        // The background image is shared by every file in the batch: load it once up front
        // (a stale/corrupt remembered path must not abort the whole batch, so it degrades
        // to the solid-color fallback inside CompositeForJpeg).
        LoadedImage? backgroundImage = null;
        if (exportOptions is
            { ExportJpeg: true, BackgroundMode: ExportBackgroundMode.Image, BackgroundImagePath: { Length: > 0 } bgPath })
        {
            try
            {
                backgroundImage = await _loader.LoadAsync(bgPath, ct);
            }
            catch
            {
                backgroundImage = null;
            }
        }

        try
        {
            // Files are independent (distinct outputs, no shared state), so process them in
            // parallel. The strategies, loader and exporter are all re-entrant per call (ONNX
            // sessions serialize their own runs, GrabCut guards its cached mask with a lock).
            // A bounded degree keeps memory in check and avoids oversubscribing CPU-bound
            // strategies that already use all cores. Counters are locked so progress stays
            // coherent when several files finish at once.
            int failed = 0;
            int skipped = 0;
            int completed = 0;
            var gate = new object();
            int maxDegree = Math.Clamp(Environment.ProcessorCount, 1, 4);

            await Parallel.ForEachAsync(inputFiles,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegree, CancellationToken = ct },
                async (file, fileCt) =>
                {
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string suffix = exportOptions is { ExportJpeg: true } ? "_cutout.jpg" : exportOptions is { ExportWebp: true } ? "_cutout.webp" : "_cutout.png";
                    string outPath = Path.Combine(outputFolder, baseName + suffix);

                    lock (gate)
                    {
                        progress?.Report(new BatchProgress(completed, inputFiles.Count, file, failed, skipped));
                    }

                    // With SkipExisting, files whose cutout already exists are left untouched so a
                    // batch can be re-run to fill in only the missing outputs (e.g. after adding
                    // new files to the input folder).
                    if (exportOptions is { SkipExisting: true } && File.Exists(outPath))
                    {
                        lock (gate)
                        {
                            skipped++;
                            completed++;
                            progress?.Report(new BatchProgress(completed, inputFiles.Count, file, failed, skipped));
                        }
                        return;
                    }

                    try
                    {
                        using var loaded = await _loader.LoadAsync(file, fileCt);
                        using var result = await strategy.RunFullAsync(loaded.FullBgr, context, fileCt);

                        if (exportOptions is { ExportJpeg: true })
                        {
                            using var composited = CompositeForJpeg(result.Bgra, exportOptions, loaded.FullBgr, backgroundImage);
                            await _exporter.ExportJpgAsync(composited, outPath, exportOptions.JpegQuality, fileCt);
                        }
                        else if (exportOptions is { ExportWebp: true })
                        {
                            // WebP keeps the transparency like PNG but is typically much smaller.
                            await _exporter.ExportWebpAsync(result.Bgra, outPath, exportOptions.JpegQuality, fileCt);
                        }
                        else
                        {
                            await _exporter.ExportPngAsync(result.Bgra, outPath, fileCt);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Skip files that fail to load/process; the batch continues with the rest.
                        lock (gate)
                        {
                            failed++;
                        }
                    }
                    finally
                    {
                        lock (gate)
                        {
                            completed++;
                            progress?.Report(new BatchProgress(completed, inputFiles.Count, file, failed, skipped));
                        }
                    }
                });

            progress?.Report(new BatchProgress(inputFiles.Count, inputFiles.Count, string.Empty, failed, skipped));
        }
        finally
        {
            backgroundImage?.Dispose();
        }
    }

    /// <summary>Composites a transparent cutout onto the requested background for JPEG output.</summary>
    private static Mat CompositeForJpeg(Mat bgra, BatchExportOptions options, Mat sourceBgr, LoadedImage? backgroundImage = null)
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
            case ExportBackgroundMode.Image:
                if (backgroundImage is not null)
                {
                    return BackgroundCompositingService.CompositeOntoImage(bgra, backgroundImage.FullBgr);
                }
                // Unloadable background: fall back to the solid color instead of failing the file.
                goto default;
            default:
                return BackgroundCompositingService.CompositeOntoColor(
                    bgra,
                    new Vec3b(options.SolidColor.B, options.SolidColor.G, options.SolidColor.R));
        }
    }
}
