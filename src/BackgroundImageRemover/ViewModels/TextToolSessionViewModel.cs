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
    private double _rotation;

    [ObservableProperty]
    private int _outlineThickness;

    [ObservableProperty]
    private WpfColor _outlineColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private bool _bold;

    [ObservableProperty]
    private int _shadowOffset;

    [ObservableProperty]
    private double _shadowOpacity = 0.5;

    [ObservableProperty]
    private bool _backgroundPlate;

    [ObservableProperty]
    private WpfColor _plateColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private double _plateOpacity = 0.5;

    [ObservableProperty]
    private int _platePadding = 10;

    [ObservableProperty]
    private bool _isOutlineColorPickerOpen;

    [ObservableProperty]
    private bool _isPlateColorPickerOpen;

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
    partial void OnRotationChanged(double value) => RefreshPreview();
    partial void OnOutlineThicknessChanged(int value) => RefreshPreview();
    partial void OnOutlineColorChanged(WpfColor value) => RefreshPreview();
    partial void OnBoldChanged(bool value) => RefreshPreview();
    partial void OnShadowOffsetChanged(int value) => RefreshPreview();
    partial void OnShadowOpacityChanged(double value) => RefreshPreview();
    partial void OnBackgroundPlateChanged(bool value) => RefreshPreview();
    partial void OnPlateColorChanged(WpfColor value) => RefreshPreview();
    partial void OnPlateOpacityChanged(double value) => RefreshPreview();
    partial void OnPlatePaddingChanged(int value) => RefreshPreview();

    private TextOverlayOptions BuildOptions() => new()
    {
        Text = Text,
        Anchor = Anchor,
        FontSize = FontSize,
        Color = new Vec3b(Color.B, Color.G, Color.R),
        Opacity = Opacity,
        Margin = Margin,
        Rotation = Rotation,
        OutlineThickness = OutlineThickness,
        OutlineColor = new Vec3b(OutlineColor.B, OutlineColor.G, OutlineColor.R),
        Bold = Bold,
        ShadowOffset = ShadowOffset,
        ShadowOpacity = ShadowOpacity,
        BackgroundPlate = BackgroundPlate,
        PlateColor = new Vec3b(PlateColor.B, PlateColor.G, PlateColor.R),
        PlateOpacity = PlateOpacity,
        PlatePadding = PlatePadding
    };

    private void RefreshPreview()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        using var rendered = TextOverlayService.Render(_sourceImage.FullBgr, BuildOptions());
        ResultBitmap = rendered.ToBitmapSource(_workingAlpha);
        IsDirty = !string.IsNullOrWhiteSpace(Text);
    }

    [RelayCommand]
    private void ClearText() => Text = string.Empty;

    public override Task ApplyAsync()
    {
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            var rendered = TextOverlayService.Render(_sourceImage.FullBgr, BuildOptions());
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
