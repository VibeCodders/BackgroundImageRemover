using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for pixelating or blurring a region (or the whole image).</summary>
public partial class MosaicToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

    public override string ToolBadge => "▦ Mosaic";
    public override string AccentColor => "#EA580C";

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private Rect? _selectedRegion;

    [ObservableProperty]
    private int _cellSize = 16;

    [ObservableProperty]
    private int _blurRadius = 20;

    [ObservableProperty]
    private bool _blurMode;

    [ObservableProperty]
    private bool _wholeImage = true;

    [ObservableProperty]
    private string? _statusMessage;

    public MosaicToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
        RefreshResult();
        StatusMessage = "Choose mosaic or blur, and a region (or the whole image).";
    }

    partial void OnCellSizeChanged(int value) => RefreshResult();
    partial void OnBlurRadiusChanged(int value) => RefreshResult();
    partial void OnBlurModeChanged(bool value) => RefreshResult();
    partial void OnWholeImageChanged(bool value) => RefreshResult();

    public void OnRectSelected(Rect rect)
    {
        SelectedRegion = rect;
        WholeImage = false;
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedRegion = null;
        WholeImage = true;
        CellSize = 16;
        BlurRadius = 20;
        BlurMode = false;
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        var region = WholeImage ? (Rect?)null : SelectedRegion;
        using var result = BlurMode
            ? MosaicService.Blur(_sourceImage.FullBgr, region, BlurRadius)
            : MosaicService.Pixelate(_sourceImage.FullBgr, region, CellSize);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = !WholeImage || CellSize > 1 || (BlurMode && BlurRadius > 0);
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        var region = WholeImage ? (Rect?)null : SelectedRegion;
        var bgr = BlurMode
            ? MosaicService.Blur(_sourceImage.FullBgr, region, BlurRadius)
            : MosaicService.Pixelate(_sourceImage.FullBgr, region, CellSize);
        _parentDocument.ApplyToolResult(bgr, _workingAlpha.Clone(), "Mosaic");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
