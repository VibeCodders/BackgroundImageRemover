using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for four-point perspective correction (keystone/straighten).</summary>
public partial class PerspectiveToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "🔲 Perspective";
    public override string AccentColor => "#0F766E";

    [ObservableProperty]
    private int _topLeftX;
    [ObservableProperty]
    private int _topLeftY;
    [ObservableProperty]
    private int _topRightX;
    [ObservableProperty]
    private int _topRightY;
    [ObservableProperty]
    private int _bottomRightX;
    [ObservableProperty]
    private int _bottomRightY;
    [ObservableProperty]
    private int _bottomLeftX;
    [ObservableProperty]
    private int _bottomLeftY;

    [ObservableProperty]
    private int _outputWidth;

    [ObservableProperty]
    private int _outputHeight;

    [ObservableProperty]
    private ResampleMethod _method = ResampleMethod.Lanczos;

    public PerspectiveToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        var quad = PerspectiveService.DefaultQuad(_sourceImage!.FullBgr.Size());
        TopLeftX = (int)quad.TopLeft.X; TopLeftY = (int)quad.TopLeft.Y;
        TopRightX = (int)quad.TopRight.X; TopRightY = (int)quad.TopRight.Y;
        BottomRightX = (int)quad.BottomRight.X; BottomRightY = (int)quad.BottomRight.Y;
        BottomLeftX = (int)quad.BottomLeft.X; BottomLeftY = (int)quad.BottomLeft.Y;
        OutputWidth = _sourceImage.FullBgr.Width;
        OutputHeight = _sourceImage.FullBgr.Height;
        RefreshResult();
        StatusMessage = "Adjust the four corners and output size.";
    }

    partial void OnTopLeftXChanged(int value) => RefreshResult();
    partial void OnTopLeftYChanged(int value) => RefreshResult();
    partial void OnTopRightXChanged(int value) => RefreshResult();
    partial void OnTopRightYChanged(int value) => RefreshResult();
    partial void OnBottomRightXChanged(int value) => RefreshResult();
    partial void OnBottomRightYChanged(int value) => RefreshResult();
    partial void OnBottomLeftXChanged(int value) => RefreshResult();
    partial void OnBottomLeftYChanged(int value) => RefreshResult();
    partial void OnOutputWidthChanged(int value) => RefreshResult();
    partial void OnOutputHeightChanged(int value) => RefreshResult();
    partial void OnMethodChanged(ResampleMethod value) => RefreshResult();

    private static InterpolationFlags ToInterpolation(ResampleMethod method)
        => ResampleMethodHelper.ToInterpolationFlags(method);

    protected override void OnReset()
    {
        if (_sourceImage is null) return;
        var quad = PerspectiveService.DefaultQuad(_sourceImage.FullBgr.Size());
        TopLeftX = (int)quad.TopLeft.X; TopLeftY = (int)quad.TopLeft.Y;
        TopRightX = (int)quad.TopRight.X; TopRightY = (int)quad.TopRight.Y;
        BottomRightX = (int)quad.BottomRight.X; BottomRightY = (int)quad.BottomRight.Y;
        BottomLeftX = (int)quad.BottomLeft.X; BottomLeftY = (int)quad.BottomLeft.Y;
        OutputWidth = _sourceImage.FullBgr.Width;
        OutputHeight = _sourceImage.FullBgr.Height;
        RefreshResult();
    }

    private void RefreshResult()
    {
        if (!EnsureSourceAlpha()) return;
        try
        {
            using var corrected = PerspectiveService.Correct(
                _sourceImage!.FullBgr,
                new Point2f(TopLeftX, TopLeftY),
                new Point2f(TopRightX, TopRightY),
                new Point2f(BottomRightX, BottomRightY),
                new Point2f(BottomLeftX, BottomLeftY),
                Math.Max(1, OutputWidth),
                Math.Max(1, OutputHeight),
                ToInterpolation(Method));
            using var correctedAlpha = PerspectiveService.Correct(
                _workingAlpha!,
                new Point2f(TopLeftX, TopLeftY),
                new Point2f(TopRightX, TopRightY),
                new Point2f(BottomRightX, BottomRightY),
                new Point2f(BottomLeftX, BottomLeftY),
                Math.Max(1, OutputWidth),
                Math.Max(1, OutputHeight),
                ToInterpolation(Method));
            ResultBitmap = corrected.ToBitmapSource(correctedAlpha);
            IsDirty = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Perspective preview failed: {ex.Message}";
        }
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var bgr = PerspectiveService.Correct(
                _sourceImage.FullBgr,
                new Point2f(TopLeftX, TopLeftY),
                new Point2f(TopRightX, TopRightY),
                new Point2f(BottomRightX, BottomRightY),
                new Point2f(BottomLeftX, BottomLeftY),
                Math.Max(1, OutputWidth),
                Math.Max(1, OutputHeight),
                ToInterpolation(Method));
            // Ownership of both Mats transfers to the document (do not dispose here).
            var alpha = PerspectiveService.Correct(
                _workingAlpha,
                new Point2f(TopLeftX, TopLeftY),
                new Point2f(TopRightX, TopRightY),
                new Point2f(BottomRightX, BottomRightY),
                new Point2f(BottomLeftX, BottomLeftY),
                Math.Max(1, OutputWidth),
                Math.Max(1, OutputHeight),
                ToInterpolation(Method));
            _parentDocument.ApplyToolResult(bgr, alpha, "Perspective");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }
}
