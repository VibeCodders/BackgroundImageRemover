using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Editing;
using BackgroundImageRemover.Services.ImageIo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Dedicated Tool Tab for placing the cutout on a new background (solid, gradient, blur,
/// image or transparent) with an optional drop shadow.
/// </summary>
public partial class ComposeToolSessionViewModel : ToolSessionViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private Mat? _workingBgr;

    // Decoded background image, cached so dragging a slider re-composites without re-reading
    // the whole file from disk on every preview tick.
    private Mat? _backgroundBgr;
    private string? _backgroundCachePath;

    public override string ToolBadge => "🖼 Compose";
    public override string AccentColor => "#0E7490";

    [ObservableProperty]
    private ExportBackgroundMode _backgroundMode = ExportBackgroundMode.SolidColor;

    [ObservableProperty]
    private WpfColor _solidColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private WpfColor _gradientTopColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private WpfColor _gradientBottomColor = WpfColor.FromRgb(120, 120, 120);

    [ObservableProperty]
    private double _blurRadius = 10;

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private bool _dropShadowEnabled;

    [ObservableProperty]
    private double _shadowDistance = 12;

    [ObservableProperty]
    private double _shadowAngle = 45;

    [ObservableProperty]
    private double _shadowBlur = 6;

    [ObservableProperty]
    private double _shadowOpacity = 0.45;

    [ObservableProperty]
    private double _subjectScale = 100.0;

    [ObservableProperty]
    private double _subjectRotation;

    [ObservableProperty]
    private int _subjectOffsetX;

    [ObservableProperty]
    private int _subjectOffsetY;

    [ObservableProperty]
    private int _backgroundPadding;

    [ObservableProperty]
    private WpfColor _shadowColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private double _gradientAngle = 90;

    [ObservableProperty]
    private BackgroundFitMode _fitMode = BackgroundFitMode.Stretch;

    [ObservableProperty]
    private double _subjectOpacity = 100.0;

    [ObservableProperty]
    private bool _subjectFlipHorizontal;

    [ObservableProperty]
    private bool _subjectFlipVertical;

    [ObservableProperty]
    private bool _isShadowColorPickerOpen;

    [ObservableProperty]
    private bool _isSolidColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientTopColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientBottomColorPickerOpen;

    public ComposeToolSessionViewModel(
        ShellViewModel shell,
        DocumentViewModel parentDocument,
        IDialogService dialogs,
        IImageLoaderService imageLoader)
        : base(shell, parentDocument)
    {
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _workingBgr = CloneWorkingBgr();
        RefreshPreview();
        StatusMessage = "Choose a background for the cutout.";
    }

    partial void OnBackgroundModeChanged(ExportBackgroundMode value) => RefreshPreview();
    partial void OnSolidColorChanged(WpfColor value) => RefreshPreview();
    partial void OnGradientTopColorChanged(WpfColor value) => RefreshPreview();
    partial void OnGradientBottomColorChanged(WpfColor value) => RefreshPreview();
    partial void OnBlurRadiusChanged(double value) => RefreshPreview();
    partial void OnBackgroundImagePathChanged(string? value) => RefreshPreview();
    partial void OnDropShadowEnabledChanged(bool value) => RefreshPreview();
    partial void OnShadowDistanceChanged(double value) => RefreshPreview();
    partial void OnShadowAngleChanged(double value) => RefreshPreview();
    partial void OnShadowBlurChanged(double value) => RefreshPreview();
    partial void OnShadowOpacityChanged(double value) => RefreshPreview();
    partial void OnSubjectScaleChanged(double value) => RefreshPreview();
    partial void OnSubjectRotationChanged(double value) => RefreshPreview();
    partial void OnSubjectOffsetXChanged(int value) => RefreshPreview();
    partial void OnSubjectOffsetYChanged(int value) => RefreshPreview();
    partial void OnBackgroundPaddingChanged(int value) => RefreshPreview();
    partial void OnShadowColorChanged(WpfColor value) => RefreshPreview();
    partial void OnGradientAngleChanged(double value) => RefreshPreview();
    partial void OnFitModeChanged(BackgroundFitMode value) => RefreshPreview();
    partial void OnSubjectOpacityChanged(double value) => RefreshPreview();
    partial void OnSubjectFlipHorizontalChanged(bool value) => RefreshPreview();
    partial void OnSubjectFlipVerticalChanged(bool value) => RefreshPreview();

    [RelayCommand]
    private async Task PickBackgroundImageAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is null)
        {
            return;
        }

        try
        {
            using var background = await _imageLoader.LoadAsync(path);
            _backgroundBgr?.Dispose();
            _backgroundBgr = background.FullBgr.Clone();
            _backgroundCachePath = path;
            BackgroundImagePath = path;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load background image: {ex.Message}";
        }
    }

    private void RefreshPreview()
    {
        if (_workingBgr is null || _workingAlpha is null || _sourceImage is null) return;

        try
        {
            using var subject = BuildSubjectBgra();
            if (BackgroundMode == ExportBackgroundMode.Transparent || (BackgroundMode == ExportBackgroundMode.Image && BackgroundImagePath is null))
            {
                ResultBitmap = subject.ToBitmapSource();
            }
            else
            {
                using var composited = CompositeOnto(subject);
                ResultBitmap = composited.ToBitmapSource();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Compose failed: {ex.Message}";
        }
    }

    private Mat BuildSubjectBgra()
    {
        using var bgra = _workingBgr!.ToBgra(_workingAlpha!);
        BackgroundCompositingService.ZeroFullyTransparentPixels(bgra);

        bool owns = true;
        var current = bgra.Clone();
        current = current.SafeChainWithCatch(r => SubjectFlipHorizontal ? TransformService.FlipHorizontal(r).Clone() : r, ref owns);
        current = current.SafeChainWithCatch(r => SubjectFlipVertical ? TransformService.FlipVertical(r).Clone() : r, ref owns);
        current = current.SafeChainWithCatch(r => Math.Abs(SubjectScale - 100.0) > 1e-4 ? TransformService.Resize(r, SubjectScale / 100.0).Clone() : r, ref owns);
        current = current.SafeChainWithCatch(r => Math.Abs(SubjectRotation) > 1e-4 ? TransformService.Rotate(r, SubjectRotation).Clone() : r, ref owns);
        current = current.SafeChainWithCatch(r => Math.Abs(SubjectOpacity - 100.0) > 1e-4 ? BackgroundCompositingService.ApplySubjectOpacity(r, SubjectOpacity / 100.0).Clone() : r, ref owns);

        if (DropShadowEnabled)
        {
            double rad = ShadowAngle * Math.PI / 180.0;
            double offsetX = ShadowDistance * Math.Cos(rad);
            double offsetY = ShadowDistance * Math.Sin(rad);
            current = current.SafeChainWithCatch(r => BackgroundCompositingService.ApplyDropShadow(
                r, offsetX, offsetY, ShadowBlur, ShadowOpacity,
                ShadowColor.ToVec3b()).Clone(), ref owns);
        }

        current = current.SafeChainWithCatch(r => BackgroundCompositingService.PlaceOnCanvas(
            r, BackgroundPadding, SubjectOffsetX, SubjectOffsetY).Clone(), ref owns);
        return current;
    }

    private Mat CompositeOnto(Mat subject)
    {
        return BackgroundMode switch
        {
            ExportBackgroundMode.SolidColor => BackgroundCompositingService.CompositeOntoColor(
                subject, SolidColor.ToVec3b()),
            ExportBackgroundMode.Gradient => BackgroundCompositingService.CompositeOntoGradient(
                subject,
                GradientTopColor.ToVec3b(),
                GradientBottomColor.ToVec3b(),
                GradientAngle),
            ExportBackgroundMode.Blur => BackgroundCompositingService.CompositeOntoBlurredImage(
                subject, _sourceImage!.FullBgr, BlurRadius),
            ExportBackgroundMode.Image => CompositeOntoImage(subject),
            _ => subject.ToBgr()
        };
    }

    private Mat CompositeOntoImage(Mat subject)
    {
        if (BackgroundImagePath is null)
        {
            return subject.ToBgr();
        }

        // Use the cached decode when available; only fall back to a blocking load when the
        // path was set without going through PickBackgroundImage (e.g. a restored session).
        if (_backgroundBgr is null || !string.Equals(_backgroundCachePath, BackgroundImagePath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var background = _imageLoader.LoadAsync(BackgroundImagePath).GetAwaiter().GetResult();
                _backgroundBgr?.Dispose();
                _backgroundBgr = background.FullBgr.Clone();
                _backgroundCachePath = BackgroundImagePath;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not load background image: {ex.Message}";
                return subject.ToBgr();
            }
        }

        var matte = SolidColor.ToVec3b();
        return BackgroundCompositingService.CompositeOntoImage(subject, _backgroundBgr, FitMode, matte);
    }

    public override Task ApplyAsync()
    {
        if (_workingBgr is null || _workingAlpha is null || _sourceImage is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        try
        {
            if (BackgroundMode == ExportBackgroundMode.Transparent && !DropShadowEnabled)
            {
                _parentDocument.ApplyToolResult(_workingBgr.Clone(), _workingAlpha.Clone(), "Compose");
            }
            else
            {
                using var subject = BuildSubjectBgra();
                if (BackgroundMode == ExportBackgroundMode.Transparent)
                {
                    var (bgr, alpha) = BackgroundCompositingService.SplitBgra(subject);
                    _parentDocument.ApplyToolResult(bgr, alpha, "Compose");
                }
                else
                {
                    var bgr = CompositeOnto(subject);
                    using var alpha = new Mat(bgr.Size(), MatType.CV_8UC1, new Scalar(255));
                    _parentDocument.ApplyToolResult(bgr, alpha, "Compose");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
            return Task.CompletedTask;
        }

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgr?.Dispose();
        _backgroundBgr?.Dispose();
        base.Dispose();
    }
}
