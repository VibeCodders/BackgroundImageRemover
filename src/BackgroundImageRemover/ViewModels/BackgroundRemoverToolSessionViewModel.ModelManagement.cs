using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.Input;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    private Task EnsureOnnxReadyAsync() => _models.EnsureOnnxReadyAsync(Onnx);

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private Task EnsureSamReadyAsync() => _models.EnsureSamReadyAsync(Sam, ComputeSamEmbedding);

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        _samEmbedding = _models.ComputeSamEmbedding();
    }
}
