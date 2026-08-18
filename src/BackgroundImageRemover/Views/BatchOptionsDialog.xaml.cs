using System.Windows;
using System.Windows.Media;
using BackgroundImageRemover.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.Views;

/// <summary>Output kinds offered by the batch dialog.</summary>
public enum BatchOutputKind
{
    Png,
    JpegWhite,
    JpegSolid,
    JpegGradient,
    JpegBlur
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
    private bool _isSolidPickerOpen;

    [ObservableProperty]
    private bool _isTopPickerOpen;

    [ObservableProperty]
    private bool _isBottomPickerOpen;

    public BatchExportOptions BuildOptions() => OutputKind switch
    {
        BatchOutputKind.Png => new BatchExportOptions { ExportJpeg = false },
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
        _ => new BatchExportOptions
        {
            ExportJpeg = true,
            JpegQuality = JpegQuality,
            BackgroundMode = ExportBackgroundMode.Blur,
            BlurRadius = 10
        }
    };
}

/// <summary>Compact dialog for choosing the batch output format and its JPEG background.</summary>
public sealed partial class BatchOptionsDialog : Window
{
    public BatchOptionsViewModel ViewModel { get; } = new();

    public BatchOptionsDialog()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public BatchExportOptions BuildOptions() => ViewModel.BuildOptions();

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void ChooseSolid_Click(object sender, RoutedEventArgs e) => ViewModel.IsSolidPickerOpen = true;
    private void ChooseTop_Click(object sender, RoutedEventArgs e) => ViewModel.IsTopPickerOpen = true;
    private void ChooseBottom_Click(object sender, RoutedEventArgs e) => ViewModel.IsBottomPickerOpen = true;
}
