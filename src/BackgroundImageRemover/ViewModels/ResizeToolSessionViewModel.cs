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
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;
    private bool _updatingSize;

    public override string ToolBadge => "⤡ Resize";
    public override string AccentColor => "#0891B2";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

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
    private string? _statusMessage;

    public ResizeToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _updatingSize = true;
        Width = _sourceImage.FullBgr.Width;
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

    [RelayCommand]
    private void UsePercent() => RefreshResult();

    [RelayCommand]
    private void PresetHalf() => Percent = 50;
    [RelayCommand]
    private void PresetDouble() => Percent = 200;
    [RelayCommand]
    private void PresetWidth1024() => Width = 1024;
    [RelayCommand]
    private void PresetWidth1920() => Width = 1920;

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;
        using var resized = ResizeService.ResizePercent(_sourceImage.FullBgr, Percent / 100.0, Method);
        ResultBitmap = resized.ToBitmapSource(_workingAlpha);
        IsDirty = true;
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        var bgr = ResizeService.ResizeTo(_sourceImage.FullBgr, Math.Max(1, Width), Math.Max(1, Height), Method);
        using var alpha = new Mat(bgr.Size(), MatType.CV_8UC1, new Scalar(255));
        _parentDocument.ApplyToolResult(bgr, alpha, "Resize");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
