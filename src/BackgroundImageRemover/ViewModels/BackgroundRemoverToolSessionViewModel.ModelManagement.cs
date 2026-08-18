using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    private async Task EnsureOnnxReadyAsync()
    {
        var model = Onnx.SelectedModel;
        Onnx.ErrorMessage = null;
        Onnx.IsDownloading = true;

        var success = await ModelDownloadHelper.EnsureOnnxModelReadyAsync(
            _onnxStrategy,
            model,
            progress => Onnx.DownloadFraction = progress,
            error => Onnx.ErrorMessage = error,
            () =>
            {
                if (model == Onnx.SelectedModel)
                {
                    Onnx.IsModelReady = true;
                    RequestPreviewDebounced();
                }
            },
            _log,
            CancellationToken.None);

        Onnx.IsDownloading = false;
    }

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private async Task EnsureSamReadyAsync()
    {
        Sam.ErrorMessage = null;
        Sam.IsDownloading = true;

        var success = await ModelDownloadHelper.EnsureSamModelReadyAsync(
            _samStrategy,
            progress => Sam.DownloadFraction = progress,
            error => Sam.ErrorMessage = error,
            () =>
            {
                Sam.IsModelReady = true;
                ComputeSamEmbedding();
            },
            _log,
            CancellationToken.None);

        Sam.IsDownloading = false;
    }

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        if (_sourceImage is null || !Sam.IsModelReady)
        {
            return;
        }
        _samEmbedding = ModelDownloadHelper.ComputeSamEmbeddingSafe(
            _samStrategy,
            _sourceImage.FullBgr,
            error => Sam.ErrorMessage = error,
            _log);
    }
}
