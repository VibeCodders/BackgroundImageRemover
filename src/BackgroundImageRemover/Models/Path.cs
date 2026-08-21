using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.Models;

public class PathObject : ObservableObject
{
    private string _name = "Path";
    private bool _isVisible = true;
    private bool _isLocked = false;
    private PathGeometry? _geometry;
    private Brush _stroke = Brushes.White;
    private double _strokeThickness = 1.0;
    private bool _isFilled = false;
    private Brush? _fill;

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

    public PathGeometry? Geometry
    {
        get => _geometry;
        set => SetProperty(ref _geometry, value);
    }

    public Brush Stroke
    {
        get => _stroke;
        set => SetProperty(ref _stroke, value);
    }

    public double StrokeThickness
    {
        get => _strokeThickness;
        set => SetProperty(ref _strokeThickness, value);
    }

    public bool IsFilled
    {
        get => _isFilled;
        set => SetProperty(ref _isFilled, value);
    }

    public Brush? Fill
    {
        get => _fill;
        set => SetProperty(ref _fill, value);
    }
}