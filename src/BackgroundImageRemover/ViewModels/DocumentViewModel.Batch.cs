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

        var inputFolder = _dialogs.ShowOpenFolderDialog("Select folder with images to process");
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

        string outputFolder = Path.Combine(inputFolder, "cutouts");
        var context = BuildContext();

        try
        {
            IsBatchRunning = true;
            var progress = new Progress<BatchProgress>(p =>
                BatchStatus = p.Completed >= p.Total ? "Batch complete." : $"Processing {p.Completed + 1}/{p.Total}: {Path.GetFileName(p.CurrentFile)}");
            await _batchProcessor.RunAsync(files, strategy, context, outputFolder, progress, CancellationToken.None);
            StatusMessage = $"Batch complete: {files.Count} image(s) exported to {outputFolder}";
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
    }
}
