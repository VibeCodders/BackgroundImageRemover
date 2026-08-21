using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.Models;

public class ImageLayer : ObservableObject
{
    private string _name = "Layer";
    private bool _isVisible = true;
    private bool _isLocked = false;
    private double _opacity = 1.0;
    private BitmapSource? _thumbnail;
    private BlendMode _blendMode = BlendMode.Normal;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    public BlendMode BlendMode
    {
        get => _blendMode;
        set => SetProperty(ref _blendMode, value);
    }
}

public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    ColorDodge,
    ColorBurn,
    HardLight,
    SoftLight,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity
}