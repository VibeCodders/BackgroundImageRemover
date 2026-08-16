using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels.StrategyViewModels;

public partial class GrabCutStrategyViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidRect))]
    private Rect? _selectedRect;

    [ObservableProperty]
    private bool _hasScribbles;

    public bool HasValidRect => SelectedRect is { Width: > 0, Height: > 0 };
}
