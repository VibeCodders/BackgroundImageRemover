using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Interface for ONNX model download and readiness operations.
/// </summary>
public interface IOnnxModelStrategy
{
    /// <summary>
    /// Checks if a specific ONNX model is ready for use.
    /// </summary>
    bool IsReady(OnnxModelKind kind);

    /// <summary>
    /// Ensures the specified ONNX model is downloaded and ready for use.
    /// </summary>
    Task EnsureReadyAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct);
}