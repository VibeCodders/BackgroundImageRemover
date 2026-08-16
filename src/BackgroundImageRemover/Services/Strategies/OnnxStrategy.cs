using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Saliency-based segmentation using a selectable ONNX model. Inference always runs at the
/// model's fixed input size, so preview and full-res runs cost about the same; only the mask's
/// feather post-processing differs by output resolution.
/// </summary>
public sealed class OnnxStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.Onnx;

    private readonly OnnxInferenceEngine _engine;

    public OnnxStrategy(OnnxInferenceEngine engine)
    {
        _engine = engine;
    }

    public bool IsReady(OnnxModelKind kind) => _engine.IsReady(kind);

    public Task EnsureReadyAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        => _engine.EnsureReadyAsync(kind, progress, ct);

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        if (!_engine.IsReady(context.OnnxModel))
        {
            throw new InvalidOperationException("ONNX model is not loaded yet.");
        }

        using var raw = _engine.InferMask(bgr, context.OnnxModel);

        var mask = new Mat();
        int feather = Math.Max(1, context.OnnxFeatherPixels);
        int kernelSize = feather * 2 + 1;
        Cv2.GaussianBlur(raw, mask, new Size(kernelSize, kernelSize), 0);
        return mask;
    }
}
