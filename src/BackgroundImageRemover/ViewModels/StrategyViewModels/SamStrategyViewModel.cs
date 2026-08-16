using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

public partial class SamStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isModelReady;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double? _downloadFraction;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasClickedPoint;
}
