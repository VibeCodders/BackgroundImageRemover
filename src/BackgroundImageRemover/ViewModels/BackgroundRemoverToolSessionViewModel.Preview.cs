using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    private void RequestPreviewDebounced() => _previews.RequestPreviewDebounced();

    private Task RunPreviewAsync() => _previews.RunPreviewAsync();

    /// <summary>True when the selected strategy has everything a preview run needs.</summary>
    private bool IsPreviewReady(StrategyKind kind) => kind switch
    {
        StrategyKind.Onnx => Onnx.IsModelReady,
        StrategyKind.Sam => Sam.IsModelReady && _samEmbedding is not null && _samPromptPointPreview is not null,
        StrategyKind.MagicWand => _magicWandSeedPreview is not null,
        _ => true
    };

    private StrategyContext BuildContext(double scaleToFull = 1.0, Mat? grabCutFg = null, Mat? grabCutBg = null)
        => StrategyContextBuilder.Build(
            this, scaleToFull, grabCutFg, grabCutBg,
            _samPromptPointPreview, _samPromptPointsPreview, _samEmbedding, _magicWandSeedPreview);

    public void OnOriginalWandClicked(Point imagePoint)
    {
        // Sample-color mode: pick the clicked pixel as the Chroma Key background color.
        if (SampleColorMode)
        {
            if (_preview is null)
            {
                return;
            }
            var px = _preview.Bgr.At<Vec3b>(imagePoint.Y, imagePoint.X);
            ChromaKey.DetectedColorBgr = new Vec3b(px.Item0, px.Item1, px.Item2);
            SampleColorMode = false;
            SelectedStrategy = StrategyKind.ChromaKey;
            RequestPreviewDebounced();
            return;
        }

        if (SelectedStrategy != StrategyKind.MagicWand)
        {
            return;
        }
        _magicWandSeedPreview = new WpfPoint(imagePoint.X, imagePoint.Y);
        MagicWand.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    public void OnOriginalSamPointClicked(Point imagePoint)
        => _models.OnSamPrimaryPointClicked(Sam, new WpfPoint(imagePoint.X, imagePoint.Y), p => _samPromptPointPreview = p);

    /// <summary>
    /// Adds an additional foreground point for SAM segmentation. Multiple points refine the
    /// selection: the primary click plus any added points all feed the decoder together.
    /// </summary>
    public void OnOriginalSamAdditionalPointClicked(Point imagePoint)
        => _models.OnSamAdditionalPointClicked(Sam, () =>
        {
            _samPromptPointsPreview ??= new List<WpfPoint>();
            _samPromptPointsPreview.Add(new WpfPoint(imagePoint.X, imagePoint.Y));
            Sam.AdditionalPointCount = _samPromptPointsPreview.Count;
        });

    /// <summary>Clears all SAM prompt points (both primary and additional).</summary>
    public void ClearSamPromptPoints()
    {
        _samPromptPointPreview = null;
        _samPromptPointsPreview?.Clear();
        Sam.AdditionalPointCount = 0;
        Sam.HasClickedPoint = false;
        RequestPreviewDebounced();
    }

    [RelayCommand]
    private async Task RefineGrabCutPreviewAsync()
    {
        if (_preview is null || !ScribbleManager.HasScribbles)
        {
            StatusMessage = "Add scribbles first.";
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Refining selection...";
            await RunPreviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refine failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
