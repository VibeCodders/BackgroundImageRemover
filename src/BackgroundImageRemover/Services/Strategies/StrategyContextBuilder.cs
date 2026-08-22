using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Sam;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Turns the current strategy settings (<see cref="IStrategyParameterSource"/>) plus the
/// transient, per-run interaction state (SAM/magic-wand click points, GrabCut scribbles -- not
/// steady VM state, so passed explicitly) into the <see cref="StrategyContext"/> a strategy
/// actually runs with. Shared by <see cref="ViewModels.DocumentViewModel"/> and
/// <see cref="ViewModels.BackgroundRemoverToolSessionViewModel"/>, which used to each carry
/// their own copy of this switch.
/// </summary>
public static class StrategyContextBuilder
{
    public static StrategyContext Build(
        IStrategyParameterSource source,
        double scaleToFull = 1.0,
        Mat? grabCutFg = null,
        Mat? grabCutBg = null,
        WpfPoint? samPromptPoint = null,
        IReadOnlyList<WpfPoint>? samPromptPoints = null,
        SamEmbedding? samEmbedding = null,
        WpfPoint? magicWandSeed = null)
    {
        var strategyContext = source.SelectedStrategy switch
        {
            StrategyKind.ChromaKey => new StrategyContext
            {
                ChromaKeyColor = source.ChromaKey.DetectedColorBgr,
                ChromaKeyTolerance = source.ChromaKey.Tolerance,
                DecontaminateEdges = source.ChromaKey.SpillSuppression
            },
            StrategyKind.GrabCut => new StrategyContext
            {
                GrabCutRect = source.GrabCut.SelectedRect is { } r
                    ? new Rect(
                        (int)Math.Round(r.X * scaleToFull),
                        (int)Math.Round(r.Y * scaleToFull),
                        (int)Math.Round(r.Width * scaleToFull),
                        (int)Math.Round(r.Height * scaleToFull))
                    : (Rect?)null,
                // The caller passes ownership-transferred snapshots (preview) or full-res
                // resized copies (apply/export) that stay valid for the whole background run --
                // never the manager's live Mats, which the UI thread may dispose mid-run.
                GrabCutForegroundScribble = grabCutFg,
                GrabCutBackgroundScribble = grabCutBg,
                GrabCutIterations = 3,
                // Scale the feather with the resolution so the export keeps the same relative
                // softness the user saw in the preview.
                GrabCutFeatherPixels = Math.Max(1, (int)Math.Round(2 * scaleToFull))
            },
            StrategyKind.Onnx => new StrategyContext
            {
                OnnxModel = source.Onnx.SelectedModel,
                OnnxFeatherPixels = (int)Math.Round(source.Onnx.FeatherPixels * scaleToFull),
                EnableAlphaMatting = source.Onnx.EnableAlphaMatting
            },
            StrategyKind.Sam => new StrategyContext
            {
                SamPromptPoint = samPromptPoint is { } p
                    ? new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))
                    : (Point?)null,
                SamPromptPoints = samPromptPoints?.Select(p =>
                    new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))).ToArray(),
                SamEmbedding = samEmbedding
            },
            StrategyKind.FloodFill => new StrategyContext
            {
                FloodFillTolerance = source.FloodFill.Tolerance
            },
            StrategyKind.KMeans => new StrategyContext
            {
                KMeansClusters = source.KMeans.ClusterCount
            },
            StrategyKind.MagicWand => new StrategyContext
            {
                MagicWandSeed = magicWandSeed is { } p
                    ? new Point((int)Math.Round(p.X * scaleToFull), (int)Math.Round(p.Y * scaleToFull))
                    : (Point?)null,
                MagicWandTolerance = source.MagicWand.Tolerance
            },
            StrategyKind.Otsu => new StrategyContext(),
            StrategyKind.Inpaint => new StrategyContext
            {
                InpaintTolerance = source.Inpaint.Tolerance,
                InpaintRadius = source.Inpaint.Radius
            },
            _ => new StrategyContext()
        };

        return strategyContext with
        {
            InvertMask = source.InvertMask,
            MaskFeatherPixels = (int)Math.Round(source.MaskFeatherPixels * scaleToFull),
            DespeckleKernelSize = (int)Math.Round(source.DespeckleKernelSize * scaleToFull),
            FillHolesKernelSize = (int)Math.Round(source.FillHolesKernelSize * scaleToFull),
            SmoothEdgesKernelSize = (int)Math.Round(source.SmoothEdgesKernelSize * scaleToFull),
            KeepLargestComponent = source.KeepLargestComponent,
            MaskExpandPixels = (int)Math.Round(source.MaskExpandPixels * scaleToFull),
            MaskBlurPixels = source.MaskBlurPixels * scaleToFull,
            MinComponentAreaPixels = (int)Math.Round(source.MinComponentAreaPixels * scaleToFull * scaleToFull),
            MaskGamma = source.MaskGamma,
            MaskHardness = source.MaskHardness,
            MaskThreshold = source.MaskThreshold,
            DespillStrength = source.DespillStrength,
            MaskMedianKernel = (int)Math.Round(source.MaskMedianKernel * scaleToFull),
            MaskBilateralKernel = (int)Math.Round(source.MaskBilateralKernel * scaleToFull),
            MaskClahe = source.MaskClahe
        };
    }
}
