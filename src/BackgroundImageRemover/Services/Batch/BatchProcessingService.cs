using System.IO;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.Services.Batch;

public readonly record struct BatchProgress(int Completed, int Total, string CurrentFile, int Failed = 0);

public interface IBatchProcessingService
{
    Task RunAsync(
        IReadOnlyList<string> inputFiles,
        IBackgroundRemovalStrategy strategy,
        StrategyContext context,
        string outputFolder,
        IProgress<BatchProgress>? progress,
        CancellationToken ct);
}

/// <summary>
/// Applies one strategy/context to every file in a list, full resolution, exporting each as
/// "<name>_cutout.png" into the output folder. Files that fail to load/process are skipped
/// (reported via progress's CurrentFile) rather than aborting the whole batch.
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
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputFolder);

        int failed = 0;
        for (int i = 0; i < inputFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string file = inputFiles[i];
            progress?.Report(new BatchProgress(i, inputFiles.Count, file, failed));

            try
            {
                using var loaded = await _loader.LoadAsync(file, ct);
                using var result = await strategy.RunFullAsync(loaded.FullBgr, context, ct);
                string outPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file) + "_cutout.png");
                await _exporter.ExportPngAsync(result.Bgra, outPath, ct);
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

        progress?.Report(new BatchProgress(inputFiles.Count, inputFiles.Count, string.Empty, failed));
    }
}
