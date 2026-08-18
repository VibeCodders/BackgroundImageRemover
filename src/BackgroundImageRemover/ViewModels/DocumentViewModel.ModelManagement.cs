using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.Input;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
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
                ExportCommand.NotifyCanExecuteChanged();
                if (_loadedImage is not null)
                {
                    ComputeSamEmbedding();
                }
            },
            _log,
            CancellationToken.None);

        Sam.IsDownloading = false;
    }

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        if (_loadedImage is null)
        {
            return;
        }
        _samEmbedding = ModelDownloadHelper.ComputeSamEmbeddingSafe(
            _samStrategy,
            _loadedImage.FullBgr,
            error => StatusMessage = $"SAM embedding failed: {error}",
            _log);
    }

    public void OnOriginalSamPointClicked(OpenCvSharp.Point previewPoint)
    {
        if (_samEmbedding is null)
        {
            StatusMessage = "SAM is still preparing this image, try again in a moment.";
            return;
        }
        _samPromptPointPreview = new WpfPoint(previewPoint.X, previewPoint.Y);
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }
}
