using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

/// <summary>User-tunable parameters for the flood-fill background removal strategy.</summary>
public partial class FloodFillStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private double _tolerance = 20;
}
