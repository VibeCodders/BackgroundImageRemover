using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

/// <summary>User-tunable parameters for the Inpaint background-removal strategy.</summary>
public partial class InpaintStrategyViewModel : ObservableObject
{
    /// <summary>Max Lab color distance from the border seed for a pixel to be flooded as background (1-100).</summary>
    [ObservableProperty]
    private double _tolerance = 20;

    /// <summary>Radius (in pixels) passed to OpenCV's Navier-Stokes inpaint algorithm (1-20).</summary>
    [ObservableProperty]
    private double _radius = 3;
}
