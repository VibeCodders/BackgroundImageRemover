using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

public partial class OnnxStrategyViewModel : ObservableObject
{
    public IReadOnlyList<OnnxModelKind> AvailableModels { get; } = Enum.GetValues<OnnxModelKind>();

    [ObservableProperty]
    private OnnxModelKind _selectedModel = OnnxModelKind.U2NetP;

    [ObservableProperty]
    private int _featherPixels = 2;

    [ObservableProperty]
    private bool _enableAlphaMatting;

    [ObservableProperty]
    private bool _isModelReady;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double? _downloadFraction;

    [ObservableProperty]
    private string? _errorMessage;
}
