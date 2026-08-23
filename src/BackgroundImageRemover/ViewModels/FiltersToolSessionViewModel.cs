using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>Dedicated Tool Tab for artistic color filters (grayscale, sepia, invert, posterize, emboss, sketch).</summary>
public partial class FiltersToolSessionViewModel : PreviewToolSessionViewModelBase
{
    public override string ToolBadge => "🎨 Filters";
    public override string AccentColor => "#D946EF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEffect))]
    private FilterKind _selectedFilter = FilterKind.Sepia;

    /// <summary>True when the selected filter actually changes the image (i.e. not "None").</summary>
    public bool HasEffect => SelectedFilter != FilterKind.None;

    [ObservableProperty]
    private double _intensity = 1.0;

    [ObservableProperty]
    private int _posterizeLevels = 4;

    protected override string OperationName => "Filters";

    protected override bool IsEffectActive => SelectedFilter != FilterKind.None && Intensity > 0.001;

    public FiltersToolSessionViewModel(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument, "Choose a filter and adjust its intensity.")
    {
        RefreshPreview();
    }

    partial void OnSelectedFilterChanged(FilterKind value) => RefreshPreview();
    partial void OnIntensityChanged(double value) => RefreshPreview();
    partial void OnPosterizeLevelsChanged(int value) => RefreshPreview();

    protected override Mat ApplyEffect(Mat bgr)
        => FilterService.Apply(bgr, SelectedFilter, Intensity, PosterizeLevels);

    protected override void OnResetDefaults()
    {
        SelectedFilter = FilterKind.None;
        Intensity = 1.0;
        PosterizeLevels = 4;
    }
}
