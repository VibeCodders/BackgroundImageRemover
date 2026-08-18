using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Sam;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Interface for SAM model download, readiness, and embedding operations.
/// </summary>
public interface ISamModelStrategy
{
    /// <summary>
    /// Checks if the SAM model is ready for use.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Ensures the SAM model is downloaded and ready for use.
    /// </summary>
    Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct);

    /// <summary>
    /// Computes the SAM embedding for the given BGR image.
    /// </summary>
    SamEmbedding ComputeEmbedding(Mat bgr);
}