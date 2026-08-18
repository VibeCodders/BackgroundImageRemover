using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

/// <summary>User-tunable parameters for the click-to-remove magic wand strategy.</summary>
public partial class MagicWandStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private double _tolerance = 20;

    [ObservableProperty]
    private bool _hasClickedPoint;
}
