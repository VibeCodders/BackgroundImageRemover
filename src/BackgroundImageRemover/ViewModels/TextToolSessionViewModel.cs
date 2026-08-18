using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for rendering a text watermark overlay.</summary>
public partial class TextToolSessionViewModel : ToolSessionViewModelBase
{
    private LoadedImage? _sourceImage;
    private Mat? _workingAlpha;

    public override string ToolBadge => "✎ Text";
    public override string AccentColor => "#DB2777";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    private string? _text = "Watermark";

    [ObservableProperty]
    private int _fontSize = 48;

    [ObservableProperty]
    private WpfColor _color = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private TextAnchor _anchor = TextAnchor.BottomRight;

    [ObservableProperty]
    private int _margin = 20;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private string? _statusMessage;

    public TextToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        _sourceImage = _parentDocument.CreateCurrentStateSnapshot();
        _workingAlpha = _sourceImage.FullAlpha?.Clone()
            ?? new Mat(_sourceImage.FullBgr.Size(), MatType.CV_8UC1, new Scalar(255));
        RefreshPreview();
        StatusMessage = "Type text and choose its position, size and color.";
    }

    partial void OnTextChanged(string? value) => RefreshPreview();
    partial void OnFontSizeChanged(int value) => RefreshPreview();
    partial void OnColorChanged(WpfColor value) => RefreshPreview();
    partial void OnOpacityChanged(double value) => RefreshPreview();
    partial void OnAnchorChanged(TextAnchor value) => RefreshPreview();
    partial void OnMarginChanged(int value) => RefreshPreview();

    private void RefreshPreview()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        using var rendered = TextOverlayService.Render(
            _sourceImage.FullBgr, Text, Anchor, FontSize, new Vec3b(Color.B, Color.G, Color.R), Opacity, Margin);
        ResultBitmap = rendered.ToBitmapSource(_workingAlpha);
        IsDirty = !string.IsNullOrWhiteSpace(Text);
    }

    [RelayCommand]
    private void ClearText() => Text = string.Empty;

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var rendered = TextOverlayService.Render(
                _sourceImage.FullBgr, Text, Anchor, FontSize, new Vec3b(Color.B, Color.G, Color.R), Opacity, Margin);
            _parentDocument.ApplyToolResult(rendered, _workingAlpha.Clone(), "Text");
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
