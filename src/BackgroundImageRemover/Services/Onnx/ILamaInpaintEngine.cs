using OpenCvSharp;

namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Runs LaMa (large-mask inpainting) inference: a single forward pass that fills the masked
/// region of a square 512×512 RGB image. The engine owns the model download/load lifecycle;
/// callers ensure readiness before <see cref="Inpaint"/>.
/// </summary>
public interface ILamaInpaintEngine : IDisposable
{
    bool IsReady { get; }

    /// <summary>
    /// Downloads (on first use) and loads the LaMa model for the requested <paramref name="variant"/>
    /// and execution provider (<paramref name="useGpu"/> requests the DirectML EP when the build
    /// includes it; it falls back to CPU if unavailable). Switching variant/GPU reloads the
    /// session. Safe to call concurrently.
    /// </summary>
    Task EnsureReadyAsync(LamaModelVariant variant, bool useGpu, IProgress<ModelDownloadProgress>? progress, CancellationToken ct);

    /// <summary>
    /// Runs the model on a square 512×512 BGR image and 512×512 byte mask (255 = inpaint,
    /// 0 = keep), returning the inpainted square 512×512 BGR image with the unmasked region
    /// preserved exactly.
    /// </summary>
    Mat Inpaint(Mat imageBgr512, Mat mask512);
}
