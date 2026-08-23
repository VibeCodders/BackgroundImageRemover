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
    [ToolParameter]
    private string? _text = "Watermark";

    [ObservableProperty]
    [ToolParameter]
    private int _fontSize = 48;

    [ObservableProperty]
    [ToolParameter]
    private WpfColor _color = WpfColor.FromRgb(255, 255, 255);

    [ObservableProperty]
    [ToolParameter]
    private double _opacity = 1.0;

    [ObservableProperty]
    [ToolParameter]
    private TextAnchor _anchor = TextAnchor.BottomRight;

    [ObservableProperty]
    [ToolParameter]
    private int _margin = 20;

    [ObservableProperty]
    [ToolParameter]
    private double _rotation;

    [ObservableProperty]
    [ToolParameter]
    private int _outlineThickness;

    [ObservableProperty]
    [ToolParameter]
    private WpfColor _outlineColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    [ToolParameter]
    private bool _bold;

    [ObservableProperty]
    [ToolParameter]
    private int _shadowOffset;

    [ObservableProperty]
    [ToolParameter]
    private double _shadowOpacity = 0.5;

    [ObservableProperty]
    [ToolParameter]
    private WpfColor _shadowColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    [ToolParameter]
    private double _shadowBlur;

    [ObservableProperty]
    [ToolParameter]
    private double _letterSpacing;

    [ObservableProperty]
    [ToolParameter]
    private int _lineSpacing;

    [ObservableProperty]
    [ToolParameter]
    private bool _autoFitWidth;

    [ObservableProperty]
    [ToolParameter]
    private bool _backgroundPlate;

    [ObservableProperty]
    [ToolParameter]
    private WpfColor _plateColor = WpfColor.FromRgb(0, 0, 0);

    [ObservableProperty]
    [ToolParameter]
    private double _plateOpacity = 0.5;

    [ObservableProperty]
    [ToolParameter]
    private int _platePadding = 10;

    protected override string OperationName => "Text";

    protected override bool IsEffectActive => !string.IsNullOrWhiteSpace(Text);

    public TextToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Type text and choose its position, size and color.")
    {
        RefreshPreview();
    }

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
