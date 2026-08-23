using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for drawing vector shapes (rectangle, ellipse, line, arrow).</summary>
public partial class ShapeToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "⬜ Shape";
    public override string AccentColor => "#10B981";

    /// <summary>The unmodified source image the user drags a shape over.</summary>
    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    [ObservableProperty]
    private ShapeKind _shapeKind = ShapeKind.Rectangle;

    // Position (top-left) and size as a percentage of the image, 0..100.
    [ObservableProperty]
    private double _positionX = 20;

    [ObservableProperty]
    private double _positionY = 20;

    [ObservableProperty]
    private double _sizeWidth = 60;

    [ObservableProperty]
    private double _sizeHeight = 60;

    [ObservableProperty]
    private int _strokeWidth = 4;

    // Sides (polygon) or points (star), shared by both point-based shapes.
    [ObservableProperty]
    private int _segments = 5;

    // Inner/outer radius ratio for the star shape.
    [ObservableProperty]
    private double _starRatio = 0.45;

    // Free rotation (degrees) applied to any closed shape about its center.
    [ObservableProperty]
    private double _rotation;

    [ObservableProperty]
    private WpfColor _strokeColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _fillEnabled;

    [ObservableProperty]
    private WpfColor _fillColor = WpfColor.FromRgb(239, 68, 68);

    [ObservableProperty]
    private double _fillOpacity = 0.5;

    protected override string OperationName => "Shape";

    protected override bool IsEffectActive => StrokeWidth > 0 || FillEnabled;

    public bool SupportsFill => ShapeKind is ShapeKind.Rectangle or ShapeKind.Ellipse or ShapeKind.Polygon or ShapeKind.Star;
    public double FillVisibility => SupportsFill ? 1 : 0;

    public bool IsStar => ShapeKind == ShapeKind.Star;
    public double StarVisibility => IsStar ? 1 : 0;

    public bool IsPointShapes => ShapeKind is ShapeKind.Polygon or ShapeKind.Star;
    public double PointShapesVisibility => IsPointShapes ? 1 : 0;

    public Array ShapeKinds => Enum.GetValues(typeof(ShapeKind));

    public ShapeToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Drag on the image to place the shape.")
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            SourceBitmap = _sourceImage.FullBgr.ToBitmapSource(_workingAlpha);
        }
        RefreshPreview();
    }

    partial void OnShapeKindChanged(ShapeKind value)
    {
        OnPropertyChanged(nameof(SupportsFill));
        OnPropertyChanged(nameof(FillVisibility));
        OnPropertyChanged(nameof(IsStar));
        OnPropertyChanged(nameof(StarVisibility));
        OnPropertyChanged(nameof(IsPointShapes));
        OnPropertyChanged(nameof(PointShapesVisibility));
        RefreshPreview();
    }

    partial void OnPositionXChanged(double value) => RequestRefresh();
    partial void OnPositionYChanged(double value) => RequestRefresh();
    partial void OnSizeWidthChanged(double value) => RequestRefresh();
    partial void OnSizeHeightChanged(double value) => RequestRefresh();
    partial void OnStrokeWidthChanged(int value) => RequestRefresh();
    partial void OnStrokeColorChanged(WpfColor value) => RequestRefresh();
    partial void OnSegmentsChanged(int value) => RequestRefresh();
    partial void OnStarRatioChanged(double value) => RequestRefresh();
    partial void OnRotationChanged(double value) => RequestRefresh();
    partial void OnFillEnabledChanged(bool value) => RequestRefresh();
    partial void OnFillColorChanged(WpfColor value) => RequestRefresh();
    partial void OnFillOpacityChanged(double value) => RequestRefresh();

    protected override Mat ApplyEffect(Mat bgr)
    {
        double w = bgr.Width;
        double h = bgr.Height;
        var rect = new Rect(
            (int)Math.Round(PositionX / 100.0 * w),
            (int)Math.Round(PositionY / 100.0 * h),
            (int)Math.Round(SizeWidth / 100.0 * w),
            (int)Math.Round(SizeHeight / 100.0 * h));

        return ShapeService.Apply(bgr, ShapeKind, rect, StrokeColor.ToVec3b(), StrokeWidth,
            FillEnabled, FillColor.ToVec3b(), FillOpacity, Segments, StarRatio, Rotation);
    }

    /// <summary>Converts a dragged rectangle (image pixels) into the position/size percentage properties.</summary>
    public void OnRectSelected(int x, int y, int width, int height)
    {
        if (_sourceImage is null)
        {
            return;
        }

        double w = _sourceImage.FullBgr.Width;
        double h = _sourceImage.FullBgr.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        PositionX = Math.Clamp(x / w * 100.0, 0.0, 100.0);
        PositionY = Math.Clamp(y / h * 100.0, 0.0, 100.0);
        SizeWidth = Math.Clamp(width / w * 100.0, 1.0, 100.0);
        SizeHeight = Math.Clamp(height / h * 100.0, 1.0, 100.0);
    }

    protected override void OnResetDefaults()
    {
        ShapeKind = ShapeKind.Rectangle;
        PositionX = 20;
        PositionY = 20;
        SizeWidth = 60;
        SizeHeight = 60;
        StrokeWidth = 4;
        Segments = 5;
        StarRatio = 0.45;
        Rotation = 0;
        FillEnabled = false;
        FillOpacity = 0.5;
    }
}
