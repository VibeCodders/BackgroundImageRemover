using System.Diagnostics;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Shared plumbing for strategies: both preview and full-res runs call the same
/// <see cref="ComputeMask"/> on a background thread and time the result; only the
/// input Mat's resolution differs between the two call sites. Optionally runs the
/// resulting mask through guided-filter alpha matting refinement.
/// </summary>
public abstract class StrategyBase : IBackgroundRemovalStrategy
{
    public abstract StrategyKind Kind { get; }

    /// <summary>Computes a single-channel 0-255 alpha mask (same size as <paramref name="bgr"/>).</summary>
    protected abstract Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct);

    /// <summary>Optional strategy-specific touch-up applied to the composited BGRA result, in-place.</summary>
    protected virtual void PostProcessBgra(Mat bgra, StrategyContext context)
    {
    }

    public Task<RemovalResult> RunPreviewAsync(Mat previewBgr, StrategyContext context, CancellationToken ct)
        => RunAsync(previewBgr, context, ct);

    public Task<RemovalResult> RunFullAsync(Mat fullBgr, StrategyContext context, CancellationToken ct)
        => RunAsync(fullBgr, context, ct);

    private Task<RemovalResult> RunAsync(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            using var rawMask = ComputeMask(bgr, context, ct);
            ct.ThrowIfCancellationRequested();

            using var mask = context.EnableAlphaMatting ? AlphaMattingRefiner.Refine(bgr, rawMask) : rawMask.Clone();

            var bgra = new Mat();
            Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
            var channels = Cv2.Split(bgra);
            try
            {
                mask.CopyTo(channels[3]);
                Cv2.Merge(channels, bgra);
            }
            finally
            {
                foreach (var c in channels)
                {
                    c.Dispose();
                }
            }

            PostProcessBgra(bgra, context);

            sw.Stop();
            return new RemovalResult(bgra, sw.Elapsed.TotalMilliseconds);
        }, ct);
    }
}
