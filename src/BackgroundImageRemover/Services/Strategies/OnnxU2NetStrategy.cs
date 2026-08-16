using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Saliency-based segmentation using the U2Netp ONNX model. Inference always runs at the
/// model's fixed 320x320 input size, so preview and full-res runs cost the same; only the
/// mask's feather post-processing differs by output resolution.
/// </summary>
public sealed class OnnxU2NetStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.Onnx;

    private readonly OnnxInferenceEngine _engine;

    public OnnxU2NetStrategy(OnnxInferenceEngine engine)
    {
        _engine = engine;
    }

    public bool IsReady => _engine.IsReady;

    public Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        => _engine.EnsureReadyAsync(progress, ct);

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        if (!_engine.IsReady)
        {
            throw new InvalidOperationException("ONNX model is not loaded yet.");
        }

        using var raw = _engine.InferMask(bgr);

        var mask = new Mat();
        int feather = Math.Max(1, context.OnnxFeatherPixels);
        int kernelSize = feather * 2 + 1;
        Cv2.GaussianBlur(raw, mask, new Size(kernelSize, kernelSize), 0);
        return mask;
    }
}
