using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

/// <summary>User-tunable parameters for the k-means background removal strategy.</summary>
public partial class KMeansStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private int _clusterCount = 4;
}
