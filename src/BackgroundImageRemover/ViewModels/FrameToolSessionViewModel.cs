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

/// <summary>Dedicated Tool Tab for adding borders, rounded corners and transparent padding.</summary>
public partial class FrameToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;

    public override string ToolBadge => "▣ Frame";
    public override string AccentColor => "#7C3AED";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private int _borderThickness = 0;

    [ObservableProperty]
    private WpfColor _borderColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private int _cornerRadius = 0;

    [ObservableProperty]
    private int _padding = 0;

    [ObservableProperty]
    private bool _isColorPickerOpen;

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
        StatusMessage = "Add a border, rounded corners or transparent padding.";
    }

    partial void OnBorderThicknessChanged(int value) => RefreshPreview();
    partial void OnBorderColorChanged(WpfColor value) => RefreshPreview();
    partial void OnCornerRadiusChanged(int value) => RefreshPreview();
    partial void OnPaddingChanged(int value) => RefreshPreview();

    private void RefreshPreview()
    {
        if (_sourceImage is null) return;

        using var alpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        using var bgra = _sourceImage.FullBgr.ToBgra(alpha);
        using var padded = FrameService.AddPadding(bgra, Padding, Padding, Padding, Padding);
        using var bordered = FrameService.AddBorder(padded, BorderThickness, new Vec3b(BorderColor.B, BorderColor.G, BorderColor.R));
        using var rounded = FrameService.RoundCorners(bordered, CornerRadius);
        ResultBitmap = rounded.ToBitmapSource();
        IsDirty = BorderThickness > 0 || CornerRadius > 0 || Padding > 0;
    }

    [RelayCommand]
    private void Reset()
    {
        BorderThickness = 0;
        CornerRadius = 0;
        Padding = 0;
        RefreshPreview();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is null)
        {
            _shell.CloseTabDirect(this);
            return Task.CompletedTask;
        }

        using var srcAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        using var bgra = _sourceImage.FullBgr.ToBgra(srcAlpha);
        using var padded = FrameService.AddPadding(bgra, Padding, Padding, Padding, Padding);
        using var bordered = FrameService.AddBorder(padded, BorderThickness, new Vec3b(BorderColor.B, BorderColor.G, BorderColor.R));
        using var rounded = FrameService.RoundCorners(bordered, CornerRadius);
        var (bgr, alpha) = BackgroundCompositingService.SplitBgra(rounded);
        _parentDocument.ApplyToolResult(bgr, alpha, "Frame");

        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose() => _sourceImage?.Dispose();
}
