using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for placing emoji-style decorative marks (stars, hearts, sparkles, etc.) on the image.</summary>
public partial class EmojiToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

    public override string ToolBadge => "🎉 Emoji";
    public override string AccentColor => "#F59E0B";

    public IReadOnlyList<EmojiOverlayService.EmojiKind> EmojiKinds { get; } =
        Enum.GetValues<EmojiOverlayService.EmojiKind>().ToArray();

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private EmojiOverlayService.EmojiKind _selectedEmoji = EmojiOverlayService.EmojiKind.Star;

    [ObservableProperty]
    private int _emojiSize = 48;

    [ObservableProperty]
    private WpfColor _emojiColor = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private bool _scatterMode;

    [ObservableProperty]
    private int _scatterCount = 20;

    [ObservableProperty]
    private int _minSize = 20;

    [ObservableProperty]
    private int _maxSize = 60;

    [ObservableProperty]
    private TextAnchor _anchor = TextAnchor.BottomRight;

    [ObservableProperty]
    private int _margin = 20;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private string? _statusMessage;

    public EmojiToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        RefreshResult();
        StatusMessage = "Choose an emoji, size and color, then apply.";
    }

    partial void OnSelectedEmojiChanged(EmojiOverlayService.EmojiKind value) => RefreshResult();
    partial void OnEmojiSizeChanged(int value) => RefreshResult();
    partial void OnEmojiColorChanged(WpfColor value) => RefreshResult();
    partial void OnOpacityChanged(double value) => RefreshResult();
    partial void OnScatterModeChanged(bool value) => RefreshResult();
    partial void OnScatterCountChanged(int value) => RefreshResult();
    partial void OnMinSizeChanged(int value) => RefreshResult();
    partial void OnMaxSizeChanged(int value) => RefreshResult();
    partial void OnAnchorChanged(TextAnchor value) => RefreshResult();
    partial void OnMarginChanged(int value) => RefreshResult();

    private Point ComputeEmojiPosition()
    {
        if (_sourceImage is null) return new Point(0, 0);

        int w = _sourceImage.FullBgr.Width;
        int h = _sourceImage.FullBgr.Height;
        int m = Margin;
        int s = EmojiSize;

        return Anchor switch
        {
            TextAnchor.TopLeft => new Point(m + s / 2, m + s / 2),
            TextAnchor.TopCenter => new Point(w / 2, m + s / 2),
            TextAnchor.TopRight => new Point(w - m - s / 2, m + s / 2),
            TextAnchor.MiddleLeft => new Point(m + s / 2, h / 2),
            TextAnchor.Center => new Point(w / 2, h / 2),
            TextAnchor.MiddleRight => new Point(w - m - s / 2, h / 2),
            TextAnchor.BottomLeft => new Point(m + s / 2, h - m - s / 2),
            TextAnchor.BottomCenter => new Point(w / 2, h - m - s / 2),
            _ => new Point(w - m - s / 2, h - m - s / 2),
        };
    }

    private void RefreshResult()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        Mat result;
        if (ScatterMode)
        {
            result = EmojiOverlayService.RenderScatter(
                _sourceImage.FullBgr, SelectedEmoji, ScatterCount, MinSize, Math.Max(MinSize, MaxSize),
                new Vec3b(EmojiColor.B, EmojiColor.G, EmojiColor.R), Opacity);
        }
        else
        {
            var pos = ComputeEmojiPosition();
            result = EmojiOverlayService.Render(
                _sourceImage.FullBgr, SelectedEmoji, new Point((int)pos.X, (int)pos.Y), EmojiSize,
                new Vec3b(EmojiColor.B, EmojiColor.G, EmojiColor.R), Opacity);
        }

        using var _ = result;
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = Opacity > 1e-4;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedEmoji = EmojiOverlayService.EmojiKind.Star;
        EmojiSize = 48;
        EmojiColor = WpfColor.FromRgb(255, 255, 255);
        Opacity = 1.0;
        ScatterMode = false;
        ScatterCount = 20;
        MinSize = 20;
        MaxSize = 60;
        Anchor = TextAnchor.BottomRight;
        Margin = 20;
        RefreshResult();
    }

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            Mat result;
            if (ScatterMode)
            {
                result = EmojiOverlayService.RenderScatter(
                    _sourceImage.FullBgr, SelectedEmoji, ScatterCount, MinSize, Math.Max(MinSize, MaxSize),
                    new Vec3b(EmojiColor.B, EmojiColor.G, EmojiColor.R), Opacity);
            }
            else
            {
                var pos = ComputeEmojiPosition();
                result = EmojiOverlayService.Render(
                    _sourceImage.FullBgr, SelectedEmoji, new Point((int)pos.X, (int)pos.Y), EmojiSize,
                    new Vec3b(EmojiColor.B, EmojiColor.G, EmojiColor.R), Opacity);
            }

            _parentDocument.ApplyToolResult(result, _workingAlpha.Clone(), "Emoji");
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _sourceImage?.Dispose();
        _workingAlpha?.Dispose();
    }
}
