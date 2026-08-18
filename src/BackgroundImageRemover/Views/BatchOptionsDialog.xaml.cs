using System.Windows;
using System.Windows.Media;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.Views;

/// <summary>Output kinds offered by the batch dialog.</summary>
public enum BatchOutputKind
{
    Png,
    Webp,
    JpegWhite,
    JpegSolid,
    JpegGradient,
    JpegBlur,
    JpegImage
}

/// <summary>Bindable state of the batch options dialog.</summary>
public sealed partial class BatchOptionsViewModel : ObservableObject
{
    [ObservableProperty]
    private BatchOutputKind _outputKind = BatchOutputKind.Png;

    [ObservableProperty]
    private Color _solidColor = Colors.White;

    [ObservableProperty]
    private Color _gradientTop = Colors.White;

    [ObservableProperty]
    private Color _gradientBottom = Color.FromRgb(120, 120, 120);

    [ObservableProperty]
    private int _jpegQuality = 95;

    [ObservableProperty]
    private bool _skipExisting;

    [ObservableProperty]
    private bool _isSolidPickerOpen;

    [ObservableProperty]
    private bool _isTopPickerOpen;

    [ObservableProperty]
    private bool _isBottomPickerOpen;

    /// <summary>Background image composited behind JPEG cutouts (only for <see cref="BatchOutputKind.JpegImage"/>).</summary>
    [ObservableProperty]
    private string? _backgroundImagePath;

    public BatchExportOptions BuildOptions()
    {
        var options = OutputKind switch
        {
            BatchOutputKind.Png => new BatchExportOptions { ExportJpeg = false },
            BatchOutputKind.Webp => new BatchExportOptions { ExportJpeg = false, ExportWebp = true, JpegQuality = JpegQuality },
            BatchOutputKind.JpegWhite => new BatchExportOptions
            {
                ExportJpeg = true,
                JpegQuality = JpegQuality,
                BackgroundMode = ExportBackgroundMode.SolidColor,
                SolidColor = Colors.White
            },
            BatchOutputKind.JpegSolid => new BatchExportOptions
            {
                ExportJpeg = true,
                JpegQuality = JpegQuality,
                BackgroundMode = ExportBackgroundMode.SolidColor,
                SolidColor = SolidColor
            },
            BatchOutputKind.JpegGradient => new BatchExportOptions
            {
                ExportJpeg = true,
                JpegQuality = JpegQuality,
                BackgroundMode = ExportBackgroundMode.Gradient,
                GradientTop = GradientTop,
                GradientBottom = GradientBottom
            },
            BatchOutputKind.JpegImage => new BatchExportOptions
            {
                ExportJpeg = true,
                JpegQuality = JpegQuality,
                BackgroundMode = ExportBackgroundMode.Image,
                BackgroundImagePath = BackgroundImagePath
            },
            _ => new BatchExportOptions
            {
                ExportJpeg = true,
                JpegQuality = JpegQuality,
                BackgroundMode = ExportBackgroundMode.Blur,
                BlurRadius = 10
            }
        };
        options.SkipExisting = SkipExisting;
        return options;
    }

    /// <summary>Restores the last session's output format and quality from persisted settings.</summary>
    public void Restore(AppSettings settings)
    {
        if (Enum.TryParse<BatchOutputKind>(settings.LastBatchOutputKind, out var kind))
        {
            OutputKind = kind;
        }
        if (settings.LastBatchJpegQuality is >= 50 and <= 100)
        {
            JpegQuality = settings.LastBatchJpegQuality;
        }
        SkipExisting = settings.LastBatchSkipExisting;
        BackgroundImagePath = settings.LastBatchBackgroundImagePath;
    }

    /// <summary>Persists the chosen format and quality so the next batch starts where this one left off.</summary>
    public void Persist(AppSettings settings)
    {
        settings.LastBatchOutputKind = OutputKind.ToString();
        settings.LastBatchJpegQuality = JpegQuality;
        settings.LastBatchSkipExisting = SkipExisting;
        settings.LastBatchBackgroundImagePath = BackgroundImagePath;
    }
}

/// <summary>Compact dialog for choosing the batch output format and its JPEG background.</summary>
public sealed partial class BatchOptionsDialog : Window
{
    public BatchOptionsViewModel ViewModel { get; } = new();

    private readonly AppSettings? _settings;

    public BatchOptionsDialog(AppSettings? settings = null)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.Restore(settings ?? new AppSettings());
    }

    public BatchExportOptions BuildOptions() => ViewModel.BuildOptions();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Persist(_settings ?? new AppSettings());
        DialogResult = true;
    }

    private void ChooseSolid_Click(object sender, RoutedEventArgs e) => ViewModel.IsSolidPickerOpen = true;
    private void ChooseTop_Click(object sender, RoutedEventArgs e) => ViewModel.IsTopPickerOpen = true;
    private void ChooseBottom_Click(object sender, RoutedEventArgs e) => ViewModel.IsBottomPickerOpen = true;

    private void ChooseBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose background image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.webp;*.gif;*.tif;*.tiff;*.ico|All files|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.BackgroundImagePath = dialog.FileName;
        }
    }
}
