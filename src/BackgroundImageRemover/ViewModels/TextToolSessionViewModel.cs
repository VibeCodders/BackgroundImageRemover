using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for rendering a text watermark overlay.</summary>
public partial class TextToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "✎ Text";
    public override string AccentColor => "#DB2777";

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
    private WpfColor _shadowColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    private double _shadowBlur;

    [ObservableProperty]
    private double _letterSpacing;

    [ObservableProperty]
    private int _lineSpacing;

    [ObservableProperty]
    private bool _autoFitWidth;

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
    private bool _isShadowColorPickerOpen;

    protected override string OperationName => "Text";

    protected override bool IsEffectActive => !string.IsNullOrWhiteSpace(Text);

    public TextToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Type text and choose its position, size and color.")
    {
        RefreshPreview();
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
    partial void OnShadowColorChanged(WpfColor value) => RefreshPreview();
    partial void OnShadowBlurChanged(double value) => RefreshPreview();
    partial void OnLetterSpacingChanged(double value) => RefreshPreview();
    partial void OnLineSpacingChanged(int value) => RefreshPreview();
    partial void OnAutoFitWidthChanged(bool value) => RefreshPreview();
    partial void OnBackgroundPlateChanged(bool value) => RefreshPreview();
    partial void OnPlateColorChanged(WpfColor value) => RefreshPreview();
    partial void OnPlateOpacityChanged(double value) => RefreshPreview();
    partial void OnPlatePaddingChanged(int value) => RefreshPreview();

    private TextOverlayOptions BuildOptions() => new()
    {
        Text = Text,
        Anchor = Anchor,
        FontSize = FontSize,
        Color = Color.ToVec3b(),
        Opacity = Opacity,
        Margin = Margin,
        Rotation = Rotation,
        OutlineThickness = OutlineThickness,
        OutlineColor = OutlineColor.ToVec3b(),
        Bold = Bold,
        ShadowOffset = ShadowOffset,
        ShadowOpacity = ShadowOpacity,
        ShadowColor = ShadowColor.ToVec3b(),
        ShadowBlur = ShadowBlur,
        LetterSpacing = LetterSpacing,
        LineSpacing = LineSpacing,
        AutoFitWidth = AutoFitWidth,
        BackgroundPlate = BackgroundPlate,
        PlateColor = PlateColor.ToVec3b(),
        PlateOpacity = PlateOpacity,
        PlatePadding = PlatePadding
    };

    protected override Mat ApplyEffect(Mat bgr)
        => TextOverlayService.Render(bgr, BuildOptions());

    [RelayCommand]
    private void ClearText() => Text = string.Empty;
}
