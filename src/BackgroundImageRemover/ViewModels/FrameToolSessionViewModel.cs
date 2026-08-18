using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for borders, rounded corners, mats, inner accents and outer shadows.</summary>
public partial class FrameToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;

    public override string ToolBadge => "▣ Frame";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private int _borderThickness;

    [ObservableProperty]
    private WpfColor _borderColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private double _borderOpacity = 1.0;

    [ObservableProperty]
    private int _cornerRadius;

    [ObservableProperty]
    private int _paddingLeft;

    [ObservableProperty]
    private int _paddingTop;

    [ObservableProperty]
    private int _paddingRight;

    [ObservableProperty]
    private int _paddingBottom;

    [ObservableProperty]
    private int _innerBorderThickness;

    [ObservableProperty]
    private WpfColor _innerBorderColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private double _innerBorderOpacity = 1.0;

    [ObservableProperty]
    private bool _useMatColor;

    [ObservableProperty]
    private WpfColor _matColor = WpfColor.FromRgb(245, 245, 245);

    [ObservableProperty]
    private bool _outerShadowEnabled;

    [ObservableProperty]
    private double _outerShadowOffset = 10;

    [ObservableProperty]
    private double _outerShadowBlur = 6;

    [ObservableProperty]
    private double _outerShadowOpacity = 0.5;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _isInnerColorPickerOpen;

    [ObservableProperty]
    private bool _isMatColorPickerOpen;

    [ObservableProperty]
    private string? _statusMessage;

    public FrameToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        RefreshPreview();
        StatusMessage = "Add borders, mats, rounded corners, inner accents and shadows.";
    }

    partial void OnBorderThicknessChanged(int value) => RefreshPreview();
    partial void OnBorderColorChanged(WpfColor value) => RefreshPreview();
    partial void OnBorderOpacityChanged(double value) => RefreshPreview();
    partial void OnCornerRadiusChanged(int value) => RefreshPreview();
    partial void OnPaddingLeftChanged(int value) => RefreshPreview();
    partial void OnPaddingTopChanged(int value) => RefreshPreview();
    partial void OnPaddingRightChanged(int value) => RefreshPreview();
    partial void OnPaddingBottomChanged(int value) => RefreshPreview();
    partial void OnInnerBorderThicknessChanged(int value) => RefreshPreview();
    partial void OnInnerBorderColorChanged(WpfColor value) => RefreshPreview();
    partial void OnInnerBorderOpacityChanged(double value) => RefreshPreview();
    partial void OnUseMatColorChanged(bool value) => RefreshPreview();
    partial void OnMatColorChanged(WpfColor value) => RefreshPreview();
    partial void OnOuterShadowEnabledChanged(bool value) => RefreshPreview();
    partial void OnOuterShadowOffsetChanged(double value) => RefreshPreview();
    partial void OnOuterShadowBlurChanged(double value) => RefreshPreview();
    partial void OnOuterShadowOpacityChanged(double value) => RefreshPreview();

    private Mat BuildFramedBgra()
    {
        using var srcAlpha = _sourceImage!.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        using var bgra = _sourceImage.FullBgr.ToBgra(srcAlpha);

        Mat current = UseMatColor
            ? FrameService.AddPaddingWithColor(
                bgra, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft,
                new Vec3b(MatColor.B, MatColor.G, MatColor.R))
            : FrameService.AddPadding(bgra, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft);

        try
        {
            using (var bordered = FrameService.AddBorder(
                current, BorderThickness, new Vec3b(BorderColor.B, BorderColor.G, BorderColor.R), BorderOpacity))
            {
                current.Dispose();
                current = bordered.Clone();
            }

            if (InnerBorderThickness > 0)
            {
                using var accented = FrameService.AddInnerBorder(
                    current, InnerBorderThickness, new Vec3b(InnerBorderColor.B, InnerBorderColor.G, InnerBorderColor.R), InnerBorderOpacity);
                current.Dispose();
                current = accented.Clone();
            }

            if (CornerRadius > 0)
            {
                using var rounded = FrameService.RoundCorners(current, CornerRadius);
                current.Dispose();
                current = rounded.Clone();
            }

            if (OuterShadowEnabled)
            {
                using var shadowed = FrameService.AddOuterShadow(current, OuterShadowOffset, OuterShadowBlur, OuterShadowOpacity);
                current.Dispose();
                current = shadowed.Clone();
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private void RefreshPreview()
    {
        if (_sourceImage is null) return;

        try
        {
            using var result = BuildFramedBgra();
            ResultBitmap = result.ToBitmapSource();
            IsDirty = BorderThickness > 0 || CornerRadius > 0 || PaddingLeft + PaddingTop + PaddingRight + PaddingBottom > 0
                || InnerBorderThickness > 0 || OuterShadowEnabled || UseMatColor;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Frame preview failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        BorderThickness = 0;
        BorderOpacity = 1.0;
        CornerRadius = 0;
        PaddingLeft = PaddingTop = PaddingRight = PaddingBottom = 0;
        InnerBorderThickness = 0;
        InnerBorderOpacity = 1.0;
        UseMatColor = false;
        OuterShadowEnabled = false;
        RefreshPreview();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var framed = BuildFramedBgra();
        var (bgr, alpha) = BackgroundCompositingService.SplitBgra(framed);
        _parentDocument.ApplyToolResult(bgr, alpha, "Frame");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose() => _sourceImage?.Dispose();
}
