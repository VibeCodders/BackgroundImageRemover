using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.ViewModels;

    /// <summary>Dedicated Tool Tab for sampling pixel colors from the image under the cursor.</summary>
    public partial class ColorPickerToolSessionViewModel : ToolSessionViewModelBase
    {
    public override string ToolBadge => "🎨 Color Picker";
    public override string AccentColor => "#0EA5E9";

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private double _brushRadius = 3;

    [ObservableProperty]
    private bool _averageSample;

    [ObservableProperty]
    private string? _hexColor = "#000000";

    [ObservableProperty]
    private int _red = 0;

    [ObservableProperty]
    private int _green = 0;

    [ObservableProperty]
    private int _blue = 0;

    [ObservableProperty]
    private double _hue;

    [ObservableProperty]
    private double _saturation;

    [ObservableProperty]
    private double _value;

    /// <summary>Formatted RGB string for display in the UI.</summary>
    public string RgbString => $"RGB({Red}, {Green}, {Blue})";

    public ColorPickerToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitFromParent();
    }

    private void InitFromParent()
    {
        InitSourceAlpha();
        PreviewBitmap = _sourceImage!.FullBgr.ToBitmapSource(_workingAlpha!);
        StatusMessage = "Click on the image to sample a color.";
    }

    public void OnImageClicked(WpfPoint imagePoint)
    {
        if (_sourceImage is null) return;

        int x = (int)Math.Round(imagePoint.X);
        int y = (int)Math.Round(imagePoint.Y);
        Vec3b bgr = AverageSample
            ? ColorPickerService.SampleAverage(_sourceImage.FullBgr, x, y, (int)BrushRadius)
            : ColorPickerService.Sample(_sourceImage.FullBgr, x, y);

        Red = bgr.Item2;   // OpenCV BGR -> display RGB
        Green = bgr.Item1;
        Blue = bgr.Item0;
        HexColor = ColorPickerService.ToHex(bgr);

        var hsv = ColorPickerService.ToHsv(bgr);
        Hue = hsv.H;
        Saturation = hsv.S;
        Value = hsv.V;

        IsDirty = true;
        OnPropertyChanged(nameof(RgbString));
        StatusMessage = $"Sampled at ({x}, {y}): {HexColor}";
    }

    [RelayCommand]
    private void CopyHex()
    {
        if (!string.IsNullOrEmpty(HexColor))
        {
            Clipboard.SetText(HexColor);
            StatusMessage = $"Copied {HexColor} to clipboard.";
        }
    }

    [RelayCommand]
    private void CopyRgb()
    {
        string rgb = RgbString;
        Clipboard.SetText(rgb);
        StatusMessage = $"Copied {rgb} to clipboard.";
    }

    public override Task ApplyAsync()
    {
        // ColorPicker doesn't modify the image; just close the tab.
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }
}
