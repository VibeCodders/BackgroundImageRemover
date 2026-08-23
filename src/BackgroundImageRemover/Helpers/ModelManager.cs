using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels.StrategyViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared ONNX/SAM model-management orchestration for the inline document editor
/// (<see cref="ViewModels.DocumentViewModel"/>) and the dedicated Background Remover tool tab
/// (<see cref="ViewModels.BackgroundRemoverToolSessionViewModel"/>). Both hosts previously
/// duplicated the download/progress/error flows, the SAM embedding computation and the SAM
/// prompt-point reset; the strategy view models, the full-resolution image source, the
/// embedding error reporter and the preview refresh are injected so behavior stays identical
/// in both hosts.
/// </summary>
public sealed class ModelManager
{
    private readonly OnnxStrategy _onnxStrategy;
    private readonly SamStrategy _samStrategy;
    private readonly IFileLogService _log;
    private readonly Func<Mat?> _imageProvider;
    private readonly Action<string> _embeddingError;
    private readonly Action _requestPreview;

    public ModelManager(
        OnnxStrategy onnxStrategy,
        SamStrategy samStrategy,
        IFileLogService log,
        Func<Mat?> imageProvider,
        Action<string> embeddingError,
        Action requestPreview)
    {
        _onnxStrategy = onnxStrategy;
        _samStrategy = samStrategy;
        _log = log;
        _imageProvider = imageProvider;
        _embeddingError = embeddingError;
        _requestPreview = requestPreview;
    }

    /// <summary>
    /// Ensures the selected ONNX model is downloaded and ready, tracking progress and error
    /// state on the given strategy view model and refreshing the preview once ready.
    /// </summary>
    public async Task EnsureOnnxReadyAsync(OnnxStrategyViewModel onnx)
    {
        var model = onnx.SelectedModel;
        onnx.ErrorMessage = null;
        onnx.IsDownloading = true;

        await ModelDownloadHelper.EnsureOnnxModelReadyAsync(
            _onnxStrategy,
            model,
            progress => onnx.DownloadFraction = progress,
            error => onnx.ErrorMessage = error,
            () =>
            {
                if (model == onnx.SelectedModel)
                {
                    onnx.IsModelReady = true;
                    _requestPreview();
                }
            },
            _log,
            CancellationToken.None);

        onnx.IsDownloading = false;
    }

    /// <summary>
    /// Ensures the SAM model is downloaded and ready, then runs <paramref name="onReady"/>
    /// (e.g. recomputing the embedding, re-enabling dependent commands).
    /// </summary>
    public async Task EnsureSamReadyAsync(SamStrategyViewModel sam, Action onReady)
    {
        sam.ErrorMessage = null;
        sam.IsDownloading = true;

        await ModelDownloadHelper.EnsureSamModelReadyAsync(
            _samStrategy,
            progress => sam.DownloadFraction = progress,
            error => sam.ErrorMessage = error,
            () =>
            {
                sam.IsModelReady = true;
                onReady();
            },
            _log,
            CancellationToken.None);

        sam.IsDownloading = false;
    }

    /// <summary>
    /// Computes the SAM embedding for the current full-resolution image, or null when no image
    /// is available. Callers are responsible for having ensured the SAM model is ready first.
    /// </summary>
    public SamEmbedding? ComputeSamEmbedding()
    {
        var image = _imageProvider();
        if (image is null)
        {
            return null;
        }
        return ModelDownloadHelper.ComputeSamEmbeddingSafe(_samStrategy, image, _embeddingError, _log);
    }

    /// <summary>
    /// Clears the SAM prompt state: runs <paramref name="clearPromptPoints"/> to reset the
    /// host's prompt points, resets the strategy view model's counters and refreshes the preview.
    /// </summary>
    public void ClearSamPromptPoints(SamStrategyViewModel sam, Action clearPromptPoints)
    {
        clearPromptPoints();
        sam.AdditionalPointCount = 0;
        sam.HasClickedPoint = false;
        _requestPreview();
    }
}
