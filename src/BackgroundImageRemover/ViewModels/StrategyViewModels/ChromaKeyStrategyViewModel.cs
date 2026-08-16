using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

public partial class ChromaKeyStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private double _tolerance = 20;

    [ObservableProperty]
    private bool _spillSuppression = true;

    [ObservableProperty]
    private Color _detectedColor = Colors.Transparent;

    private Vec3b? _detectedColorBgr;

    public Vec3b? DetectedColorBgr
    {
        get => _detectedColorBgr;
        set
        {
            _detectedColorBgr = value;
            if (value is { } c)
            {
                DetectedColor = Color.FromRgb(c.Item2, c.Item1, c.Item0);
            }
        }
    }
}
