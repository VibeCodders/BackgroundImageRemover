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
    private MosaicMode _mode = MosaicMode.Pixelate;

    [ObservableProperty]
    private bool _invertRegion;

    [ObservableProperty]
    private double _strength = 1.0;

    [ObservableProperty]
    private int _jitter = 6;

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
    partial void OnModeChanged(MosaicMode value) => RefreshResult();
    partial void OnInvertRegionChanged(bool value) => RefreshResult();
    partial void OnStrengthChanged(double value) => RefreshResult();
    partial void OnJitterChanged(int value) => RefreshResult();
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
        Mode = MosaicMode.Pixelate;
        InvertRegion = false;
        Strength = 1.0;
        Jitter = 6;
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = BuildResult(_sourceImage.FullBgr);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = !WholeImage
            || CellSize > 1
            || BlurRadius > 0
            || Mode is MosaicMode.Median or MosaicMode.SolidFill or MosaicMode.Crystallize
            || InvertRegion
            || Math.Abs(Strength - 1.0) > 1e-4;
    }

    private Mat BuildResult(Mat src)
    {
        var region = WholeImage ? (Rect?)null : SelectedRegion;

        if (InvertRegion && region is { } r)
        {
            var bounds = MosaicService.ClampRegion(src.Size(), r);
            using var outside = ApplyModeCore(src, null);
            using var original = new Mat(src, bounds);
            using var dest = new Mat(outside, bounds);
            original.CopyTo(dest);
            return outside.Clone();
        }

        return ApplyModeCore(src, region);
    }

    private Mat ApplyModeCore(Mat src, Rect? region) => Mode switch
    {
        MosaicMode.Blur => Strength < 0.999
            ? MosaicService.BlurSoft(src, region, BlurRadius, Strength)
            : MosaicService.Blur(src, region, BlurRadius),
        MosaicMode.Median => MosaicService.MedianBlur(src, region, BlurRadius),
        MosaicMode.SolidFill => MosaicService.SolidFill(src, region, new Vec3b(0, 0, 0)),
        MosaicMode.Crystallize => MosaicService.Crystallize(src, region, CellSize, Jitter),
        _ => MosaicService.Pixelate(src, region, CellSize)
    };

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        var bgr = BuildResult(_sourceImage.FullBgr);
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
