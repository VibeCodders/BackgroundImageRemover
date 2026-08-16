using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels.StrategyViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IDownscaleService _downscaler;
    private readonly IDialogService _dialogs;
    private readonly IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxU2NetStrategy _onnxStrategy;

    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _applyCts;

    private LoadedImage? _loadedImage;
    private PreviewImage? _preview;
    private RemovalResult? _lastPreviewResult;
    private RemovalResult? _lastFullResult;

    public ChromaKeyStrategyViewModel ChromaKey { get; } = new();
    public GrabCutStrategyViewModel GrabCut { get; } = new();
    public OnnxStrategyViewModel Onnx { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private StrategyKind _selectedStrategy = StrategyKind.ChromaKey;

    [ObservableProperty]
    private BitmapSource? _previewBitmap;

    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isImageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public MainViewModel(
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IDownscaleService downscaler,
        IDialogService dialogs,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxU2NetStrategy onnxStrategy)
    {
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _downscaler = downscaler;
        _dialogs = dialogs;
        _strategies = strategies.ToDictionary(s => s.Kind);
        _onnxStrategy = onnxStrategy;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = RunPreviewAsync();
        };

        ChromaKey.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChromaKey.Tolerance))
            {
                RequestPreviewDebounced();
            }
        };

        GrabCut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrabCut.SelectedRect))
            {
                ApplyCommand.NotifyCanExecuteChanged();
                if (GrabCut.HasValidRect)
                {
                    RequestPreviewDebounced();
                }
            }
        };

        Onnx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Onnx.IsModelReady))
            {
                ApplyCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName == nameof(Onnx.FeatherPixels) && Onnx.IsModelReady)
            {
                RequestPreviewDebounced();
            }
        };
    }

    partial void OnSelectedStrategyChanged(StrategyKind value)
    {
        if (value == StrategyKind.Onnx && !Onnx.IsModelReady)
        {
            _ = EnsureOnnxReadyAsync();
        }
        else
        {
            RequestPreviewDebounced();
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = _dialogs.ShowOpenImageDialog();
        if (path is not null)
        {
            await LoadImageAsync(path);
        }
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Loading image...";

            _loadedImage?.Dispose();
            _preview?.Dispose();

            _loadedImage = await _imageLoader.LoadAsync(path);
            _preview = _downscaler.CreatePreview(_loadedImage.FullBgr);

            PreviewBitmap = _preview.Bgr.ToBitmapSource();
            ResultBitmap = null;
            IsImageLoaded = true;
            StatusMessage = $"Loaded {Path.GetFileName(path)} ({_loadedImage.FullBgr.Width}x{_loadedImage.FullBgr.Height})";

            GrabCut.SelectedRect = null;
            ChromaKey.DetectedColorBgr = ChromaKeyStrategy.DetectDominantBorderColor(_preview.Bgr);

            RequestPreviewDebounced();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load image: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureOnnxReadyAsync()
    {
        try
        {
            Onnx.ErrorMessage = null;
            Onnx.IsDownloading = true;
            var progress = new Progress<Services.Onnx.ModelDownloadProgress>(p => Onnx.DownloadFraction = p.FractionComplete);
            await _onnxStrategy.EnsureReadyAsync(progress, CancellationToken.None);
            Onnx.IsModelReady = true;
            RequestPreviewDebounced();
        }
        catch (Exception ex)
        {
            Onnx.ErrorMessage = $"Could not download model: {ex.Message}";
        }
        finally
        {
            Onnx.IsDownloading = false;
        }
    }

    [RelayCommand]
    private Task RetryOnnxDownloadAsync() => EnsureOnnxReadyAsync();

    private void RequestPreviewDebounced()
    {
        if (!IsImageLoaded)
        {
            return;
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private StrategyContext BuildContext(double scaleToFull = 1.0)
    {
        return SelectedStrategy switch
        {
            StrategyKind.ChromaKey => new StrategyContext
            {
                ChromaKeyColor = ChromaKey.DetectedColorBgr,
                ChromaKeyTolerance = ChromaKey.Tolerance
            },
            StrategyKind.GrabCut => new StrategyContext
            {
                GrabCutRect = GrabCut.SelectedRect is { } r
                    ? new Rect(
                        (int)Math.Round(r.X * scaleToFull),
                        (int)Math.Round(r.Y * scaleToFull),
                        (int)Math.Round(r.Width * scaleToFull),
                        (int)Math.Round(r.Height * scaleToFull))
                    : (Rect?)null,
                GrabCutIterations = scaleToFull > 1.0 ? 5 : 3
            },
            StrategyKind.Onnx => new StrategyContext
            {
                OnnxFeatherPixels = Onnx.FeatherPixels
            },
            _ => new StrategyContext()
        };
    }

    private async Task RunPreviewAsync()
    {
        if (_preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        if (SelectedStrategy == StrategyKind.GrabCut && !GrabCut.HasValidRect)
        {
            return;
        }
        if (SelectedStrategy == StrategyKind.Onnx && !Onnx.IsModelReady)
        {
            return;
        }

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        try
        {
            var context = BuildContext();
            var result = await strategy.RunPreviewAsync(_preview.Bgr, context, cts.Token);

            if (cts.IsCancellationRequested)
            {
                result.Dispose();
                return;
            }

            _lastPreviewResult?.Dispose();
            _lastPreviewResult = result;
            ResultBitmap = result.Bgra.ToBitmapSource();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer preview request
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    private bool CanApply() => IsImageLoaded && !IsBusy
        && (SelectedStrategy != StrategyKind.GrabCut || GrabCut.HasValidRect)
        && (SelectedStrategy != StrategyKind.Onnx || Onnx.IsModelReady);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (_loadedImage is null || _preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            return;
        }

        _applyCts?.Cancel();
        var cts = new CancellationTokenSource();
        _applyCts = cts;

        try
        {
            IsBusy = true;
            BusyMessage = "Processing at full resolution...";

            var context = BuildContext(_preview.ScaleFactor);
            var result = await strategy.RunFullAsync(_loadedImage.FullBgr, context, cts.Token);

            _lastFullResult?.Dispose();
            _lastFullResult = result;
            ResultBitmap = result.Bgra.ToBitmapSource();
            StatusMessage = $"Processed in {result.ElapsedMilliseconds:F0} ms";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Processing failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_lastFullResult is null)
        {
            StatusMessage = "Run Apply before exporting.";
            return;
        }

        var suggested = _loadedImage is not null
            ? Path.GetFileNameWithoutExtension(_loadedImage.FilePath) + "_cutout.png"
            : "cutout.png";

        var path = _dialogs.ShowSavePngDialog(suggested);
        if (path is null)
        {
            return;
        }

        try
        {
            await _imageExporter.ExportPngAsync(_lastFullResult.Bgra, path);
            StatusMessage = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _loadedImage?.Dispose();
        _preview?.Dispose();
        _lastPreviewResult?.Dispose();
        _lastFullResult?.Dispose();
    }
}
