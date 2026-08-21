using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for artistic color filters (grayscale, sepia, invert, posterize, emboss, sketch).</summary>
public partial class FiltersToolSessionViewModel : ToolSessionViewModelBase
{
    public override string ToolBadge => "🎨 Filters";
    public override string AccentColor => "#D946EF";

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEffect))]
    private FilterKind _selectedFilter = FilterKind.Sepia;

    /// <summary>True when the selected filter actually changes the image (i.e. not "None").</summary>
    public bool HasEffect => SelectedFilter != FilterKind.None;

    [ObservableProperty]
    private double _intensity = 1.0;

    [ObservableProperty]
    private int _posterizeLevels = 4;

    [ObservableProperty]
    private string? _statusMessage;

    public FiltersToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
        InitSourceAlpha();
        RefreshPreview();
        StatusMessage = "Choose a filter and adjust its intensity.";
    }

    partial void OnSelectedFilterChanged(FilterKind value) => RefreshPreview();
    partial void OnIntensityChanged(double value) => RefreshPreview();
    partial void OnPosterizeLevelsChanged(int value) => RefreshPreview();

    private void RefreshPreview()
    {
        if (_sourceImage is null || _workingAlpha is null) return;

        using var filtered = FilterService.Apply(_sourceImage.FullBgr, SelectedFilter, Intensity, PosterizeLevels);
        ResultBitmap = filtered.ToBitmapSource(_workingAlpha);
        IsDirty = SelectedFilter != FilterKind.None && Intensity > 0.001;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedFilter = FilterKind.None;
        Intensity = 1.0;
        PosterizeLevels = 4;
        RefreshPreview();
    }

    public override Task ApplyAsync()
    {
        Mat? filtered = null;
        if (_sourceImage is not null && _workingAlpha is not null)
        {
            filtered = FilterService.Apply(_sourceImage.FullBgr, SelectedFilter, Intensity, PosterizeLevels);
        }
        ApplyAndClose(filtered, "Filters");
        return Task.CompletedTask;
    }
}
