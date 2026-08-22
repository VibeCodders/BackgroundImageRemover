using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for resizing (exact size, aspect lock, percent, interpolation).</summary>
public partial class ResizeToolSessionViewModel : ToolSessionViewModelBase
{
    private bool _updatingSize;

    public override string ToolBadge => "⤡ Resize";
    public override string AccentColor => "#0891B2";

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private bool _keepAspect = true;

    [ObservableProperty]
    private double _percent = 100.0;

    [ObservableProperty]
    private ResampleMethod _method = ResampleMethod.Lanczos;

    [ObservableProperty]
    private ResizeMode _mode = ResizeMode.ExactSize;

    [ObservableProperty]
    private int _fitWidth = 1024;

    [ObservableProperty]
    private int _fitHeight = 1024;

    [ObservableProperty]
    private int _longestSide = 1024;

    [ObservableProperty]
    private double _megapixels = 1.0;

    public ResizeToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        _updatingSize = true;
        Width = _sourceImage!.FullBgr.Width;
        Height = _sourceImage.FullBgr.Height;
        _updatingSize = false;
        RefreshResult();
        StatusMessage = "Set a size or a percentage, then apply.";
    }

    partial void OnWidthChanged(int value)
    {
        if (_updatingSize || !KeepAspect || _sourceImage is null || Width <= 0) return;
        _updatingSize = true;
        Height = Math.Max(1, (int)Math.Round((double)_sourceImage.FullBgr.Height * Width / _sourceImage.FullBgr.Width));
        _updatingSize = false;
        RefreshResult();
    }

    partial void OnHeightChanged(int value)
    {
        if (_updatingSize || !KeepAspect || _sourceImage is null || Height <= 0) return;
        _updatingSize = true;
        Width = Math.Max(1, (int)Math.Round((double)_sourceImage.FullBgr.Width * Height / _sourceImage.FullBgr.Height));
        _updatingSize = false;
        RefreshResult();
    }

    partial void OnPercentChanged(double value) => RefreshResult();
    partial void OnMethodChanged(ResampleMethod value) => RefreshResult();
    partial void OnModeChanged(ResizeMode value) => RefreshResult();
    partial void OnFitWidthChanged(int value) => RefreshResult();
    partial void OnFitHeightChanged(int value) => RefreshResult();
    partial void OnLongestSideChanged(int value) => RefreshResult();
    partial void OnMegapixelsChanged(double value) => RefreshResult();

    [RelayCommand]
    private void UsePercent() => Mode = ResizeMode.Percent;

    [RelayCommand]
    private void PresetHalf() { Percent = 50; Mode = ResizeMode.Percent; }
    [RelayCommand]
    private void PresetDouble() { Percent = 200; Mode = ResizeMode.Percent; }
    [RelayCommand]
    private void PresetWidth1024() { Width = 1024; Mode = ResizeMode.ExactSize; }
    [RelayCommand]
    private void PresetWidth1920() { Width = 1920; Mode = ResizeMode.ExactSize; }

    private void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;
        using var resized = BuildResult(_sourceImage!.FullBgr);
        using var alpha = BuildAlphaFor(resized.Size());
        ResultBitmap = resized.ToBitmapSource(alpha);
        IsDirty = true;
    }

    /// <summary>Returns the source alpha scaled to the new image size (or an opaque mask when
    /// the source had no transparency), so resizing a cutout keeps its transparency intact.</summary>
    private Mat BuildAlphaFor(Size size)
    {
        // Callers guard _workingAlpha; keep a safe opaque fallback for the compiler.
        if (_workingAlpha is null)
        {
            return new Mat(size, MatType.CV_8UC1, new Scalar(255));
        }
        if (_workingAlpha.Size() == size)
        {
            return _workingAlpha.Clone();
        }
        return ResizeService.ResizeTo(_workingAlpha, size.Width, size.Height, Method);
    }

    private Mat BuildResult(Mat src) => Mode switch
    {
        ResizeMode.Percent => ResizeService.ResizePercent(src, Percent / 100.0, Method),
        ResizeMode.FitWithin => ResizeService.FitWithin(src, FitWidth, FitHeight, Method),
        ResizeMode.FillTo => ResizeService.FillTo(src, FitWidth, FitHeight, Method),
        ResizeMode.LongestSide => ResizeService.ResizeToLongestSide(src, LongestSide, Method),
        ResizeMode.Megapixels => ResizeService.ResizeToMegapixels(src, Megapixels, Method),
        _ => ResizeService.ResizeTo(src, Math.Max(1, Width), Math.Max(1, Height), Method)
    };

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var bgr = BuildResult(_sourceImage.FullBgr);
            var alpha = BuildAlphaFor(bgr.Size());
            _parentDocument.ApplyToolResult(bgr, alpha, "Resize");
        }

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}