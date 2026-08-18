using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private string? _batchStatus;

    private bool CanBatch() => IsImageLoaded && !IsBusy && SelectedStrategy is not (StrategyKind.GrabCut or StrategyKind.Sam)
        && (SelectedStrategy != StrategyKind.Onnx || Onnx.IsModelReady);

    [RelayCommand(CanExecute = nameof(CanBatch))]
    private async Task BatchAsync()
    {
        if (!_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        // Start the pickers at the folders used last time, when available.
        var rememberedInput = _settings.Current.LastBatchInputFolder ?? _settings.Current.LastBatchOutputFolder;
        var inputFolder = _dialogs.ShowOpenFolderDialog("Select folder with images to process", rememberedInput);
        if (inputFolder is null)
        {
            return;
        }

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".webp", ".gif", ".tif", ".tiff", ".ico" };
        var files = Directory.EnumerateFiles(inputFolder)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (files.Count == 0)
        {
            StatusMessage = "No supported images found in that folder.";
            return;
        }

        // Ask for the output format (PNG cutouts vs JPEG composited onto a background).
        var exportOptions = _dialogs.ShowBatchOptionsDialog();
        if (exportOptions is null)
        {
            return;
        }

        // Let the user pick where the cutouts go; default to the last used output folder or
        // the input folder's "cutouts" subfolder.
        var defaultOutput = _settings.Current.LastBatchOutputFolder ?? Path.Combine(inputFolder, "cutouts");
        var outputFolder = _dialogs.ShowOpenFolderDialog("Select output folder for cutouts", defaultOutput) ?? defaultOutput;
        var context = BuildContext();

        BatchProgress? lastReported = null;
        try
        {
            IsBatchRunning = true;
            var progress = new Progress<BatchProgress>(p =>
            {
                lastReported = p;
                BatchStatus = p.Completed >= p.Total
                    ? "Batch complete."
                    : $"Processing {p.Completed + 1}/{p.Total}: {Path.GetFileName(p.CurrentFile)}";
            });
            await _batchProcessor.RunAsync(files, strategy, context, outputFolder, progress, CancellationToken.None, exportOptions);

            int failed = lastReported?.Failed ?? 0;
            string format = exportOptions.ExportJpeg ? "JPEG" : "PNG";
            var summary = $"Batch complete: {files.Count - failed}/{files.Count} {format} image(s) exported to {outputFolder}";
            StatusMessage = failed > 0 ? summary + $" ({failed} failed)" : summary;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch failed: {ex.Message}";
            _log.Error("Batch failed", ex);
        }
        finally
        {
            IsBatchRunning = false;
        }

        // Remember the folders for the next batch run.
        _settings.Current.LastBatchInputFolder = inputFolder;
        _settings.Current.LastBatchOutputFolder = outputFolder;
        _settings.Save();
    }
}
