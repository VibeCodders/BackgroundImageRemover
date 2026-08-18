using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
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

        // Post-fill finishing applied to the whole result.
        public int CornerRadius { get; init; }
        public int BorderThickness { get; init; }
        public WpfColor BorderColor { get; init; } = WpfColor.FromRgb(255, 255, 255);
        public double GrainAmount { get; init; }
        public bool FlipHorizontal { get; init; }
        public bool FlipVertical { get; init; }
        public double RotateAngle { get; init; }
        public double Vignette { get; init; }
        public double Saturation { get; init; } = 1.0;
        public double Contrast { get; init; } = 1.0;
        public double Brightness { get; init; }
        public double SharpenStrength { get; init; }
        public int FinishBlurRadius { get; init; }
        public double Temperature { get; init; }
        public double Tint { get; init; }
        public double Denoise { get; init; }
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
    /// Applies the post-fill finishing options (flip, grain, border, rounded corners) to a
    /// filled BGR image and returns the finished BGRA image.
    /// </summary>
    public static Mat ApplyFinishing(Mat filledBgr, UncropConfig config)
    {
        var current = filledBgr.Clone();
        try
        {
            if (config.FlipHorizontal)
            {
                var flipped = TransformService.FlipHorizontal(current);
                current.Dispose();
                current = flipped;
            }
            if (config.FlipVertical)
            {
                var flipped = TransformService.FlipVertical(current);
                current.Dispose();
                current = flipped;
            }
            if (config.GrainAmount > 1e-4)
            {
                var grained = AddGrain(current, config.GrainAmount);
                current.Dispose();
                current = grained;
            }
            if (Math.Abs(config.RotateAngle) > 1e-4)
            {
                var rotated = TransformService.Rotate(current, config.RotateAngle);
                current.Dispose();
                current = rotated;
            }

            var adjustments = new ImageAdjustments
            {
                Brightness = config.Brightness,
                Contrast = config.Contrast,
                Saturation = config.Saturation,
                Vignette = config.Vignette,
                SharpenStrength = config.SharpenStrength,
                BlurRadius = config.FinishBlurRadius,
                Temperature = config.Temperature,
                Tint = config.Tint,
                Denoise = config.Denoise
            };
            if (!adjustments.IsIdentity)
            {
                var adjusted = ImageProcessingHelper.ApplyAdjustments(current, adjustments);
                current.Dispose();
                current = adjusted;
            }

            using var bgra = current.ToBgra();
            using var bordered = config.BorderThickness > 0
                ? FrameService.AddBorder(bgra, config.BorderThickness, new Vec3b(config.BorderColor.B, config.BorderColor.G, config.BorderColor.R))
                : bgra.Clone();
            using var rounded = config.CornerRadius > 0
                ? FrameService.RoundCorners(bordered, config.CornerRadius)
                : bordered.Clone();
            return rounded.Clone();
        }
        finally
        {
            current.Dispose();
        }
    }

    /// <summary>Adds subtle Gaussian grain to a BGR image, scaled by <paramref name="amount"/> (0..1).</summary>
    public static Mat AddGrain(Mat bgr, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        if (amount <= 1e-4)
        {
            return bgr.Clone();
        }

        using var noise = new Mat(bgr.Size(), MatType.CV_32FC3);
        Cv2.Randn(noise, Scalar.All(0), Scalar.All(30.0 * amount));
        using var bgrF = new Mat();
        bgr.ConvertTo(bgrF, MatType.CV_32FC3);
        using var noisyF = new Mat();
        Cv2.Add(bgrF, noise, noisyF);
        var result = new Mat();
        noisyF.ConvertTo(result, MatType.CV_8UC3);
        return result;
    }

    /// <summary>
    /// Checks if an uncrop operation can be executed with the given configuration.
    /// </summary>
    public static bool CanExecute(UncropConfig config)
    {
        return config.FillMode != UncropFillMode.AiOutpaint && !config.Padding.IsZero;
    }
}