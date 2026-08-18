using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Helper class to reduce code duplication for model download operations.
/// Handles the common pattern of downloading models with progress reporting and error handling.
/// </summary>
public static class ModelDownloadHelper
{
    /// <summary>
    /// Ensures an ONNX model is ready for use, handling download progress and error states.
    /// </summary>
    /// <param name="strategy">The ONNX strategy to use for downloading</param>
    /// <param name="model">The model kind to download</param>
    /// <param name="onDownloadProgress">Action to report download progress (0.0 to 1.0)</param>
    /// <param name="onError">Action to handle error messages</param>
    /// <param name="onSuccess">Action to call when download succeeds</param>
    /// <param name="logService">Optional logging service</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public static async Task<bool> EnsureOnnxModelReadyAsync(
        IOnnxModelStrategy strategy,
        OnnxModelKind model,
        Action<double> onDownloadProgress,
        Action<string> onError,
        Action onSuccess,
        IFileLogService? logService = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var progress = new Progress<ModelDownloadProgress>(p => onDownloadProgress(p.FractionComplete ?? 0.0));
            await strategy.EnsureReadyAsync(model, progress, cancellationToken);
            onSuccess();
            return true;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Could not download model: {ex.Message}";
            onError(errorMsg);
            logService?.Error("ONNX model download failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Ensures the SAM model is ready for use, handling download progress and error states.
    /// </summary>
    /// <param name="strategy">The SAM strategy to use for downloading</param>
    /// <param name="onDownloadProgress">Action to report download progress (0.0 to 1.0)</param>
    /// <param name="onError">Action to handle error messages</param>
    /// <param name="onSuccess">Action to call when download succeeds</param>
    /// <param name="logService">Optional logging service</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public static async Task<bool> EnsureSamModelReadyAsync(
        ISamModelStrategy strategy,
        Action<double> onDownloadProgress,
        Action<string> onError,
        Action onSuccess,
        IFileLogService? logService = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var progress = new Progress<ModelDownloadProgress>(p => onDownloadProgress(p.FractionComplete ?? 0.0));
            await strategy.EnsureReadyAsync(progress, cancellationToken);
            onSuccess();
            return true;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Could not download SAM model: {ex.Message}";
            onError(errorMsg);
            logService?.Error("SAM model download failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Computes SAM embedding with error handling.
    /// </summary>
    /// <param name="strategy">The SAM strategy to use for embedding computation</param>
    /// <param name="imageBgr">The BGR image to compute embedding for</param>
    /// <param name="onError">Action to handle error messages</param>
    /// <param name="logService">Optional logging service</param>
    /// <returns>The computed embedding, or null if computation failed</returns>
    public static SamEmbedding? ComputeSamEmbeddingSafe(
        ISamModelStrategy strategy,
        OpenCvSharp.Mat imageBgr,
        Action<string> onError,
        IFileLogService? logService = null)
    {
        try
        {
            return strategy.ComputeEmbedding(imageBgr);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Embedding failed: {ex.Message}";
            onError(errorMsg);
            logService?.Error("SAM embedding computation failed", ex);
            return null;
        }
    }
}