using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Sam;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Click-to-select segmentation using MobileSAM. The (expensive) image embedding is computed
/// once per image by the caller and passed in via <see cref="StrategyContext.SamEmbedding"/>;
/// this strategy only runs the cheap decoder per click, so preview and full-res runs are both fast.
/// </summary>
public sealed class SamStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.Sam;

    private readonly SamInferenceEngine _engine;

    public SamStrategy(SamInferenceEngine engine)
    {
        _engine = engine;
    }

    public bool IsReady => _engine.IsReady;

    public Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        => _engine.EnsureReadyAsync(progress, ct);

    public SamEmbedding ComputeEmbedding(Mat bgr) => _engine.ComputeEmbedding(bgr);

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        if (context.SamPromptPoint is not { } point || context.SamEmbedding is not SamEmbedding embedding)
        {
            throw new InvalidOperationException("SAM requires a clicked point and a computed image embedding.");
        }

        return _engine.InferMask(embedding, bgr.Size(), point);
    }
}
