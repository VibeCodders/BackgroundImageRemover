using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for drawing vector shapes (rectangle, ellipse, line, arrow).</summary>
public partial class ShapeToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "⬜ Shape";
    public override string AccentColor => "#10B981";

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

    [ObservableProperty]
    private WpfColor _strokeColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _fillEnabled;

    [ObservableProperty]
    private WpfColor _fillColor = WpfColor.FromRgb(239, 68, 68);

    [ObservableProperty]
    private double _fillOpacity = 0.5;

    [ObservableProperty]
    private bool _isStrokeColorPickerOpen;

    [ObservableProperty]
    private bool _isFillColorPickerOpen;

    protected override string OperationName => "Shape";

    protected override bool IsEffectActive => StrokeWidth > 0 || FillEnabled;

    public bool SupportsFill => ShapeKind == ShapeKind.Rectangle || ShapeKind == ShapeKind.Ellipse;
    public double FillVisibility => SupportsFill ? 1 : 0;

    public Array ShapeKinds => Enum.GetValues(typeof(ShapeKind));

    public ShapeToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Draw a rectangle, ellipse, line or arrow.")
    {
        RefreshPreview();
    }

    partial void OnShapeKindChanged(ShapeKind value)
    {
        OnPropertyChanged(nameof(SupportsFill));
        OnPropertyChanged(nameof(FillVisibility));
        RefreshPreview();
    }

    partial void OnPositionXChanged(double value) => RefreshPreview();
    partial void OnPositionYChanged(double value) => RefreshPreview();
    partial void OnSizeWidthChanged(double value) => RefreshPreview();
    partial void OnSizeHeightChanged(double value) => RefreshPreview();
    partial void OnStrokeWidthChanged(int value) => RefreshPreview();
    partial void OnStrokeColorChanged(WpfColor value) => RefreshPreview();
    partial void OnFillEnabledChanged(bool value) => RefreshPreview();
    partial void OnFillColorChanged(WpfColor value) => RefreshPreview();
    partial void OnFillOpacityChanged(double value) => RefreshPreview();

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
            FillEnabled, FillColor.ToVec3b(), FillOpacity);
    }

    [RelayCommand]
    private void Reset()
    {
        ShapeKind = ShapeKind.Rectangle;
        PositionX = 20;
        PositionY = 20;
        SizeWidth = 60;
        SizeHeight = 60;
        StrokeWidth = 4;
        FillEnabled = false;
        FillOpacity = 0.5;
        RefreshPreview();
    }
}
