using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

/// <summary>
/// Extends an image beyond its borders with LaMa (AI inpainting): expands the canvas, marks the
/// newly added area as the inpaint mask, runs the model and composites the result. The model is
/// downloaded on first use (size depends on the selected <paramref name="model"/> variant);
/// <paramref name="downloadProgress"/> reports that download. <paramref name="useGpu"/> requests
/// the DirectML execution provider when the build includes it (falls back to CPU otherwise).
/// </summary>
public interface IAiOutpaintService
{
    Task<Mat> OutpaintAsync(Mat sourceBgr, CanvasPadding padding, LamaModelVariant model, bool useGpu, IProgress<ModelDownloadProgress>? downloadProgress, CancellationToken ct);
}
