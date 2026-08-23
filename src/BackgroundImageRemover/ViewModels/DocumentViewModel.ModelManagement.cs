using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.Input;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

public partial class DocumentViewModel
{
    private Task EnsureOnnxReadyAsync() => _models.EnsureOnnxReadyAsync(Onnx);

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private Task EnsureSamReadyAsync() => _models.EnsureSamReadyAsync(Sam, OnSamReady);

    private void OnSamReady()
    {
        ExportCommand.NotifyCanExecuteChanged();
        if (_loadedImage is not null)
        {
            ComputeSamEmbedding();
        }
    }

    [RelayCommand]
    private Task RetrySamDownloadAsync() => EnsureSamReadyAsync();

    private void ComputeSamEmbedding()
    {
        _samEmbedding = _models.ComputeSamEmbedding();
    }

    public void OnOriginalSamPointClicked(OpenCvSharp.Point previewPoint)
    {
        if (_samEmbedding is null)
        {
            StatusMessage = "SAM is still preparing this image, try again in a moment.";
            return;
        }
        _samPromptPointPreview = new WpfPoint(previewPoint.X, previewPoint.Y);
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    /// <summary>
    /// Adds an additional foreground point for SAM segmentation in the main editor.
    /// Multiple points refine the selection: the primary click plus any added points
    /// all feed the decoder together.
    /// </summary>
    public void OnOriginalSamAdditionalPointClicked(OpenCvSharp.Point previewPoint)
    {
        if (_samEmbedding is null)
        {
            StatusMessage = "SAM is still preparing this image, try again in a moment.";
            return;
        }
        _samPromptPointsPreview ??= new List<WpfPoint>();
        _samPromptPointsPreview.Add(new WpfPoint(previewPoint.X, previewPoint.Y));
        Sam.AdditionalPointCount = _samPromptPointsPreview.Count;
        Sam.HasClickedPoint = true;
        RequestPreviewDebounced();
    }

    /// <summary>Clears all SAM prompt points (both primary and additional).</summary>
    public void ClearSamPromptPoints()
    {
        _models.ClearSamPromptPoints(Sam, () =>
        {
            _samPromptPointPreview = null;
            _samPromptPointsPreview?.Clear();
        });
    }

    public void OnOriginalWandClicked(OpenCvSharp.Point previewPoint)
    {
        if (SelectedStrategy != StrategyKind.MagicWand)
        {
            return;
        }
        _magicWandSeedPreview = new WpfPoint(previewPoint.X, previewPoint.Y);
        MagicWand.HasClickedPoint = true;
        RequestPreviewDebounced();
    }
}
