using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

public interface IBackgroundRemovalStrategy
{
    StrategyKind Kind { get; }

    Task<RemovalResult> RunPreviewAsync(Mat previewBgr, StrategyContext context, CancellationToken ct);

    Task<RemovalResult> RunFullAsync(Mat fullBgr, StrategyContext context, CancellationToken ct);
}
