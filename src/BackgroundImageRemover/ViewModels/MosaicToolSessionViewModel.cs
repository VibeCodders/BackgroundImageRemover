using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for pixelating or blurring a region (or the whole image).</summary>
public partial class MosaicToolSessionViewModel : MaskToolSessionViewModelBase
{
    public override string ToolBadge => "▦ Mosaic";
    public override string AccentColor => "#EA580C";

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
    private bool _hasPaintedMask;

    public InteractionMode PreviewMode => PaintMode ? InteractionMode.Brush : InteractionMode.DrawRect;

    protected override string OperationName => "Mosaic";

    public MosaicToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitMask();
        StatusMessage = "Choose mosaic or blur, then paint or select a region.";
    }

    partial void OnCellSizeChanged(int value) => RefreshResult();
    partial void OnBlurRadiusChanged(int value) => RefreshResult();
    partial void OnModeChanged(MosaicMode value) => RefreshResult();
    partial void OnInvertRegionChanged(bool value) => RefreshResult();
    partial void OnStrengthChanged(double value) => RefreshResult();
    partial void OnJitterChanged(int value) => RefreshResult();
    partial void OnFillColorChanged(WpfColor value) => RefreshResult();

    public void OnRectSelected(Rect rect)
    {
        SelectedRegion = rect;
        WholeImage = false;
        RefreshResult();
    }

    public override void OnBrushStrokeEnd()
    {
        base.OnBrushStrokeEnd();
        HasPaintedMask = _paintedMask is not null && Cv2.CountNonZero(_paintedMask) > 0;
    }

    protected override void RefreshResult()
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

    protected override Mat BuildResult(Mat src)
    {
        if (PaintMode)
        {
            return BuildMaskedResult(src);
        }

        var region = WholeImage ? (Rect?)null : SelectedRegion;

        if (InvertRegion && region is { } r)
        {
            var bounds = GeometryHelper.ClampToSize(src.Size(), r);
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
}
