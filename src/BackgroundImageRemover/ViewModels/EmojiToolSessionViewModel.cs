using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for placing emoji-style decorative marks (stars, hearts, sparkles, etc.) on the image.</summary>
public partial class EmojiToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🎉 Emoji";
    public override string AccentColor => "#F59E0B";

    public IReadOnlyList<EmojiOverlayService.EmojiKind> EmojiKinds { get; } =
        Enum.GetValues<EmojiOverlayService.EmojiKind>().ToArray();

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

    protected override string OperationName => "Emoji";

    protected override bool IsEffectActive => Opacity > 1e-4;

    public EmojiToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Choose an emoji, size and color, then apply.")
    {
        RefreshPreview();
    }

    partial void OnSelectedEmojiChanged(EmojiOverlayService.EmojiKind value) => RefreshPreview();
    partial void OnEmojiSizeChanged(int value) => RefreshPreview();
    partial void OnEmojiColorChanged(WpfColor value) => RefreshPreview();
    partial void OnOpacityChanged(double value) => RefreshPreview();
    partial void OnScatterModeChanged(bool value) => RefreshPreview();
    partial void OnScatterCountChanged(int value) => RefreshPreview();
    partial void OnMinSizeChanged(int value) => RefreshPreview();
    partial void OnMaxSizeChanged(int value) => RefreshPreview();
    partial void OnAnchorChanged(TextAnchor value) => RefreshPreview();
    partial void OnMarginChanged(int value) => RefreshPreview();

    private Point ComputeEmojiPosition(Mat image)
    {
        int w = image.Width;
        int h = image.Height;
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

    protected override Mat ApplyEffect(Mat bgr)
    {
        var color = EmojiColor.ToVec3b();
        if (ScatterMode)
        {
            return EmojiOverlayService.RenderScatter(
                bgr, SelectedEmoji, ScatterCount, MinSize, Math.Max(MinSize, MaxSize), color, Opacity);
        }

        var pos = ComputeEmojiPosition(bgr);
        return EmojiOverlayService.Render(bgr, SelectedEmoji, new Point((int)pos.X, (int)pos.Y), EmojiSize, color, Opacity);
    }

    protected override void OnResetDefaults()
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
    }
}
