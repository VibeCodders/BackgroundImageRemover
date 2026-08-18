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
    private LoadedImage? _sourceImage;
    private Mat? _workingBgr;
    private Mat? _workingAlpha;

    public override string ToolBadge => "🖼 Compose";
    public override string AccentColor => "#0E7490";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

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
    private double _shadowOffset = 12;

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
    private bool _isShadowColorPickerOpen;

    [ObservableProperty]
    private bool _isSolidColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientTopColorPickerOpen;

    [ObservableProperty]
    private bool _isGradientBottomColorPickerOpen;

    [ObservableProperty]
    private string? _statusMessage;

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
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingBgr = _sourceImage.FullBgr.Clone();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_workingBgr.Size(), MatType.CV_8UC1, new Scalar(255));
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
    partial void OnShadowOffsetChanged(double value) => RefreshPreview();
    partial void OnShadowBlurChanged(double value) => RefreshPreview();
    partial void OnShadowOpacityChanged(double value) => RefreshPreview();
    partial void OnSubjectScaleChanged(double value) => RefreshPreview();
    partial void OnSubjectRotationChanged(double value) => RefreshPreview();
    partial void OnSubjectOffsetXChanged(int value) => RefreshPreview();
    partial void OnSubjectOffsetYChanged(int value) => RefreshPreview();
    partial void OnBackgroundPaddingChanged(int value) => RefreshPreview();
    partial void OnShadowColorChanged(WpfColor value) => RefreshPreview();

    [RelayCommand]
    private void PickBackgroundImage()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            BackgroundImagePath = path;
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

        Mat current = Math.Abs(SubjectScale - 100.0) > 1e-4
            ? TransformService.Resize(bgra, SubjectScale / 100.0)
            : bgra.Clone();

        try
        {
            if (Math.Abs(SubjectRotation) > 1e-4)
            {
                using var rotated = TransformService.Rotate(current, SubjectRotation);
                current.Dispose();
                current = rotated.Clone();
            }

            if (DropShadowEnabled)
            {
                using var shadowed = BackgroundCompositingService.ApplyDropShadow(
                    current, ShadowOffset, ShadowOffset, ShadowBlur, ShadowOpacity,
                    new Vec3b(ShadowColor.B, ShadowColor.G, ShadowColor.R));
                current.Dispose();
                current = shadowed.Clone();
            }

            using var placed = BackgroundCompositingService.PlaceOnCanvas(
                current, BackgroundPadding, SubjectOffsetX, SubjectOffsetY);
            current.Dispose();
            return placed.Clone();
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private Mat CompositeOnto(Mat subject)
    {
        return BackgroundMode switch
        {
            ExportBackgroundMode.SolidColor => BackgroundCompositingService.CompositeOntoColor(
                subject, new Vec3b(SolidColor.B, SolidColor.G, SolidColor.R)),
            ExportBackgroundMode.Gradient => BackgroundCompositingService.CompositeOntoGradient(
                subject,
                new Vec3b(GradientTopColor.B, GradientTopColor.G, GradientTopColor.R),
                new Vec3b(GradientBottomColor.B, GradientBottomColor.G, GradientBottomColor.R)),
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
        using var background = _imageLoader.LoadAsync(BackgroundImagePath).GetAwaiter().GetResult();
        return BackgroundCompositingService.CompositeOntoImage(subject, background.FullBgr);
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
        _sourceImage?.Dispose();
        _workingBgr?.Dispose();
        _workingAlpha?.Dispose();
    }
}
