using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Outpaint;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Helper class to reduce code duplication for uncrop operations.
/// Centralizes the parameter extraction and service call pattern used in both DocumentViewModel and UncropViewModel.
/// </summary>
public static class UncropOperationHelper
{
    /// <summary>
    /// Configuration for an uncrop operation.
    /// </summary>
    public class UncropConfig
    {
        public CanvasPadding Padding { get; init; } = CanvasPadding.Zero;
        public UncropFillMode FillMode { get; init; } = UncropFillMode.Mirror;
        public UncropMirrorType MirrorType { get; init; } = UncropMirrorType.Reflect101;
        public int MirrorBlurRadius { get; init; }
        public double MirrorFadeOpacity { get; init; } = 1.0;
        public UncropInpaintMethod InpaintMethod { get; init; } = UncropInpaintMethod.Telea;
        public double InpaintRadius { get; init; } = 5.0;
        public int BlendMargin { get; init; }
        public bool InpaintPreFillEdgeAverage { get; init; }
        public bool BlurredColorFill { get; init; }
        public int BlurRadius { get; init; }
        public int ReplicateSmoothRadius { get; init; }
        public int ZoomBlurRadius { get; init; } = 35;
        public double ZoomScale { get; init; } = 1.25;
        public UncropGradientMode GradientMode { get; init; } = UncropGradientMode.PerEdgeSplay;
        public double GradientNoiseAmount { get; init; }
        public int PatchSize { get; init; } = 32;
        public int PatchBlendOverlap { get; init; } = 8;
        public UncropColorSource ColorSource { get; init; } = UncropColorSource.EdgeAverage;
        public WpfColor CustomSolidColor { get; init; } = WpfColor.FromRgb(255, 255, 255);
    }

    /// <summary>
    /// Executes an uncrop operation using the provided configuration and service.
    /// </summary>
    /// <param name="sourceBgr">The source BGR image to expand</param>
    /// <param name="config">The uncrop configuration</param>
    /// <param name="fillService">The uncrop fill service</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The filled BGR image</returns>
    public static async Task<Mat> ExecuteUncropAsync(
        Mat sourceBgr,
        UncropConfig config,
        IUncropFillService fillService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceBgr);
        ArgumentNullException.ThrowIfNull(fillService);

        var customColor = config.ColorSource == UncropColorSource.CustomColor
            ? new Scalar(config.CustomSolidColor.B, config.CustomSolidColor.G, config.CustomSolidColor.R)
            : (Scalar?)null;

        return await Task.Run(() => config.FillMode switch
        {
            UncropFillMode.Mirror => fillService.FillMirror(
                sourceBgr, config.Padding, config.MirrorType, config.MirrorBlurRadius, config.MirrorFadeOpacity, cancellationToken),

            UncropFillMode.Inpaint => fillService.FillInpaint(
                sourceBgr, config.Padding, config.InpaintMethod, config.InpaintRadius, config.BlendMargin, config.InpaintPreFillEdgeAverage, cancellationToken),

            UncropFillMode.SolidColor => fillService.FillSolidColor(
                sourceBgr, config.Padding, config.BlurredColorFill, customColor, config.BlurRadius, cancellationToken),

            UncropFillMode.Replicate => fillService.FillReplicate(
                sourceBgr, config.Padding, config.ReplicateSmoothRadius, cancellationToken),

            UncropFillMode.Wrap => fillService.FillWrap(
                sourceBgr, config.Padding, cancellationToken),

            UncropFillMode.ZoomBlur => fillService.FillZoomBlur(
                sourceBgr, config.Padding, config.ZoomBlurRadius, config.ZoomScale, config.BlendMargin, cancellationToken),

            UncropFillMode.EdgeGradient => fillService.FillEdgeGradient(
                sourceBgr, config.Padding, config.GradientMode, customColor, config.GradientNoiseAmount, cancellationToken),

            UncropFillMode.PatchSynthesis => fillService.FillPatchSynthesis(
                sourceBgr, config.Padding, config.PatchSize, config.PatchBlendOverlap, config.BlendMargin, cancellationToken),

            _ => throw new InvalidOperationException($"Fill mode {config.FillMode} is not available.")
        }, cancellationToken);
    }

    /// <summary>
    /// Checks if an uncrop operation can be executed with the given configuration.
    /// </summary>
    public static bool CanExecute(UncropConfig config)
    {
        return config.FillMode != UncropFillMode.AiOutpaint && !config.Padding.IsZero;
    }
}