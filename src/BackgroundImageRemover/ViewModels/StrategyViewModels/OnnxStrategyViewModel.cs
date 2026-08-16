using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

public partial class OnnxStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    private int _featherPixels = 2;

    [ObservableProperty]
    private bool _isModelReady;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double? _downloadFraction;

    [ObservableProperty]
    private string? _errorMessage;
}
