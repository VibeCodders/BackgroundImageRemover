using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using WpfColor = System.Windows.Media.Color;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for pixelating or blurring a region (or the whole image).</summary>
public partial class MosaicToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;
    private Mat? _paintedMask;

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
    private WpfColor _fillColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private bool _isFillColorPickerOpen;

    [ObservableProperty]
    private bool _wholeImage = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewMode))]
    private bool _paintMode;

    [ObservableProperty]
    private double _brushRadius = 30;

    [ObservableProperty]
    private bool _hasPaintedMask;

    public InteractionMode PreviewMode => PaintMode ? InteractionMode.Brush : InteractionMode.DrawRect;

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
        _paintedMask = new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
        RefreshResult();
        StatusMessage = "Choose mosaic or blur, then paint or select a region.";
    }

    partial void OnCellSizeChanged(int value) => RefreshResult();
    partial void OnBlurRadiusChanged(int value) => RefreshResult();
    partial void OnModeChanged(MosaicMode value) => RefreshResult();
    partial void OnInvertRegionChanged(bool value) => RefreshResult();
    partial void OnStrengthChanged(double value) => RefreshResult();
    partial void OnJitterChanged(int value) => RefreshResult();
    partial void OnFillColorChanged(WpfColor value) => RefreshResult();
    partial void OnWholeImageChanged(bool value) => RefreshResult();
    partial void OnPaintModeChanged(bool value) => RefreshResult();

    public void OnRectSelected(Rect rect)
    {
        SelectedRegion = rect;
        WholeImage = false;
        RefreshResult();
    }

    public void OnBrushStrokeStart(WpfPoint imagePoint, double pixelRadius)
        => StampMask(imagePoint, imagePoint, pixelRadius);

    public void OnBrushStrokeMove(WpfPoint imagePoint, double pixelRadius)
        => StampMask(imagePoint, imagePoint, pixelRadius);

    public void OnBrushStrokeEnd()
    {
        HasPaintedMask = _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0;
        RefreshResult();
    }

    private void StampMask(WpfPoint from, WpfPoint to, double pixelRadius)
    {
        if (_paintedMask is null) return;
        int r = Math.Max(1, (int)Math.Round(pixelRadius));
        Cv2.Line(_paintedMask, new Point((int)from.X, (int)from.Y), new Point((int)to.X, (int)to.Y), Scalar.All(255), r * 2);
        Cv2.Circle(_paintedMask, new Point((int)to.X, (int)to.Y), r, Scalar.All(255), -1);
        HasPaintedMask = true;
    }

    [RelayCommand]
    private void ClearMask()
    {
        _paintedMask?.SetTo(Scalar.All(0));
        HasPaintedMask = false;
        RefreshResult();
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedRegion = null;
        WholeImage = true;
        PaintMode = false;
        _paintedMask?.SetTo(Scalar.All(0));
        HasPaintedMask = false;
        CellSize = 16;
        BlurRadius = 20;
        Mode = MosaicMode.Pixelate;
        InvertRegion = false;
        Strength = 1.0;
        Jitter = 6;
        FillColor = WpfColor.FromRgb(0, 0, 0);
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var result = BuildResult(_sourceImage.FullBgr);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = !WholeImage
            || HasPaintedMask
            || CellSize > 1
            || BlurRadius > 0
            || Mode is MosaicMode.Median or MosaicMode.SolidFill or MosaicMode.Crystallize
            || InvertRegion
            || Math.Abs(Strength - 1.0) > 1e-4;
    }

    private Mat BuildResult(Mat src)
    {
        if (PaintMode)
        {
            return BuildMaskedResult(src);
        }

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

    private Mat BuildMaskedResult(Mat src)
    {
        if (_paintedMask is null || Cv2.CountNonZero(_paintedMask) == 0)
        {
            return src.Clone();
        }

        using var effect = ApplyModeCore(src, null);
        return InvertRegion
            ? MosaicService.BlendByMask(effect, src, _paintedMask)
            : MosaicService.BlendByMask(src, effect, _paintedMask);
    }

    private Mat ApplyModeCore(Mat src, Rect? region) => Mode switch
    {
        MosaicMode.Blur => Strength < 0.999
            ? MosaicService.BlurSoft(src, region, BlurRadius, Strength)
            : MosaicService.Blur(src, region, BlurRadius),
        MosaicMode.Median => MosaicService.MedianBlur(src, region, BlurRadius),
        MosaicMode.SolidFill => MosaicService.SolidFill(src, region, new Vec3b(FillColor.B, FillColor.G, FillColor.R)),
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
        _paintedMask?.Dispose();
    }
}
