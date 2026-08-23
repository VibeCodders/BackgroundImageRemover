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

    public override string ToolBadge => "▣ Frame";
    public override string AccentColor => "#7C3AED";

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
    private bool _gradientBorderEnabled;

    [ObservableProperty]
    private WpfColor _gradientBorderColorA = WpfColor.FromRgb(255, 0, 0);

    [ObservableProperty]
    private WpfColor _gradientBorderColorB = WpfColor.FromRgb(0, 0, 255);

    [ObservableProperty]
    private bool _borderTop = true;

    [ObservableProperty]
    private bool _borderRight = true;

    [ObservableProperty]
    private bool _borderBottom = true;

    [ObservableProperty]
    private bool _borderLeft = true;

    [ObservableProperty]
    private bool _bevelEnabled;

    [ObservableProperty]
    private int _bevelThickness;

    [ObservableProperty]
    private double _bevelOpacity = 1.0;

    [ObservableProperty]
    private bool _polaroidEnabled;

    [ObservableProperty]
    private int _polaroidHeight = 60;

    [ObservableProperty]
    private WpfColor _polaroidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private bool _vignetteEnabled;

    [ObservableProperty]
    private double _vignetteStrength = 0.5;

    [ObservableProperty]
    private WpfColor _vignetteColor = WpfColor.FromRgb(0, 0, 0);

    public FrameToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
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
    partial void OnGradientBorderEnabledChanged(bool value) => RefreshPreview();
    partial void OnGradientBorderColorAChanged(WpfColor value) => RefreshPreview();
    partial void OnGradientBorderColorBChanged(WpfColor value) => RefreshPreview();
    partial void OnBorderTopChanged(bool value) => RefreshPreview();
    partial void OnBorderRightChanged(bool value) => RefreshPreview();
    partial void OnBorderBottomChanged(bool value) => RefreshPreview();
    partial void OnBorderLeftChanged(bool value) => RefreshPreview();
    partial void OnBevelEnabledChanged(bool value) => RefreshPreview();
    partial void OnBevelThicknessChanged(int value) => RefreshPreview();
    partial void OnBevelOpacityChanged(double value) => RefreshPreview();
    partial void OnPolaroidEnabledChanged(bool value) => RefreshPreview();
    partial void OnPolaroidHeightChanged(int value) => RefreshPreview();
    partial void OnPolaroidColorChanged(WpfColor value) => RefreshPreview();
    partial void OnVignetteEnabledChanged(bool value) => RefreshPreview();
    partial void OnVignetteStrengthChanged(double value) => RefreshPreview();
    partial void OnVignetteColorChanged(WpfColor value) => RefreshPreview();

    private Mat BuildFramedBgra()
    {
        using var srcAlpha = _sourceImage!.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        using var bgra = _sourceImage.FullBgr.ToBgra(srcAlpha);

        Mat current = UseMatColor
            ? FrameService.AddPaddingWithColor(
                bgra, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft,
                MatColor.ToVec3b())
            : FrameService.AddPadding(bgra, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft);

        try
        {
            if (BorderThickness > 0)
            {
                using var bordered = GradientBorderEnabled
                    ? FrameService.AddGradientBorder(
                        current, BorderThickness,
                        GradientBorderColorA.ToVec3b(),
                        GradientBorderColorB.ToVec3b(),
                        BorderOpacity)
                    : BorderTop && BorderRight && BorderBottom && BorderLeft
                        ? FrameService.AddBorder(
                            current, BorderThickness, BorderColor.ToVec3b(), BorderOpacity)
                        : FrameService.AddPartialBorder(
                            current, BorderThickness, BorderColor.ToVec3b(), BorderOpacity,
                            BorderTop, BorderRight, BorderBottom, BorderLeft);
                current.Dispose();
                current = bordered.Clone();
            }

            if (BevelEnabled && BevelThickness > 0)
            {
                using var beveled = FrameService.AddBevel(
                    current, BevelThickness,
                    new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), BevelOpacity);
                current.Dispose();
                current = beveled.Clone();
            }

            if (InnerBorderThickness > 0)
            {
                using var accented = FrameService.AddInnerBorder(
                    current, InnerBorderThickness, InnerBorderColor.ToVec3b(), InnerBorderOpacity);
                current.Dispose();
                current = accented.Clone();
            }

            if (CornerRadius > 0)
            {
                using var rounded = FrameService.RoundCorners(current, CornerRadius);
                current.Dispose();
                current = rounded.Clone();
            }

            if (PolaroidEnabled)
            {
                using var polaroid = FrameService.AddPolaroidBar(
                    current, PolaroidHeight, PolaroidColor.ToVec3b());
                current.Dispose();
                current = polaroid.Clone();
            }

            if (VignetteEnabled)
            {
                using var vignetted = FrameService.AddVignette(
                    current, VignetteStrength, VignetteColor.ToVec3b());
                current.Dispose();
                current = vignetted.Clone();
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
                || InnerBorderThickness > 0 || OuterShadowEnabled || UseMatColor
                || GradientBorderEnabled || BevelEnabled || PolaroidEnabled || VignetteEnabled
                || !BorderTop || !BorderRight || !BorderBottom || !BorderLeft;
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
        GradientBorderEnabled = false;
        BorderTop = BorderRight = BorderBottom = BorderLeft = true;
        BevelEnabled = false;
        BevelThickness = 0;
        BevelOpacity = 1.0;
        PolaroidEnabled = false;
        PolaroidHeight = 60;
        VignetteEnabled = false;
        VignetteStrength = 0.5;
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
}
