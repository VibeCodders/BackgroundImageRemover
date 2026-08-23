using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared preview orchestration for the inline document editor
/// (<see cref="ViewModels.DocumentViewModel"/>) and the dedicated Background Remover tool tab
/// (<see cref="ViewModels.BackgroundRemoverToolSessionViewModel"/>). Both hosts previously
/// duplicated the debounced scheduling, the strategy readiness guards, the scribble/preview
/// snapshotting, the cancellation-token lifecycle and the error handling of a preview run; the
/// host-specific state (preview Mat, strategies, readiness, scribble manager, context builder,
/// result/status setters) is injected so behavior stays identical in both hosts.
/// </summary>
public sealed class PreviewRunner : IDisposable
{
    private readonly DispatcherTimer _debounceTimer;
    private readonly Func<bool>? _canRequestPreview;
    private readonly Func<PreviewImage?> _preview;
    private readonly IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> _strategies;
    private readonly Func<StrategyKind> _selectedStrategy;
    private readonly Func<StrategyKind, bool> _isReady;
    private readonly Func<ScribbleManager> _scribbles;
    private readonly Func<Mat?, Mat?, StrategyContext> _buildContext;
    private readonly Action<BitmapSource> _setResult;
    private readonly Action<string> _setStatus;
    private readonly Action _onPreviewCompleted;

    private CancellationTokenSource? _previewCts;
    private RemovalResult? _lastPreviewResult;

    /// <param name="preview">Provides the current preview image (null until the host has one).</param>
    /// <param name="strategies">The strategy registry used to resolve the selected strategy.</param>
    /// <param name="selectedStrategy">Provides the currently selected <see cref="StrategyKind"/>.</param>
    /// <param name="isReady">True when the selected strategy has everything it needs for a preview run
    /// (model downloaded, seed point placed, ...).</param>
    /// <param name="scribbles">Provides the host's <see cref="ScribbleManager"/> for snapshotting.</param>
    /// <param name="buildContext">Builds the <see cref="StrategyContext"/> from the foreground/background
    /// scribble snapshots plus the host's SAM/wand seed state.</param>
    /// <param name="setResult">Assigns the rendered preview <see cref="BitmapSource"/> to the host's result property.</param>
    /// <param name="setStatus">Reports errors to the host's status message.</param>
    /// <param name="onPreviewCompleted">Hook invoked after a successful run (e.g. marking the session dirty).</param>
    /// <param name="canRequestPreview">Optional gate for <see cref="RequestPreviewDebounced"/> (e.g. the document
    /// editor skips scheduling while a result-edit mode is active).</param>
    public PreviewRunner(
        Func<PreviewImage?> preview,
        IReadOnlyDictionary<StrategyKind, IBackgroundRemovalStrategy> strategies,
        Func<StrategyKind> selectedStrategy,
        Func<StrategyKind, bool> isReady,
        Func<ScribbleManager> scribbles,
        Func<Mat?, Mat?, StrategyContext> buildContext,
        Action<BitmapSource> setResult,
        Action<string> setStatus,
        Action onPreviewCompleted,
        Func<bool>? canRequestPreview = null)
    {
        _preview = preview;
        _strategies = strategies;
        _selectedStrategy = selectedStrategy;
        _isReady = isReady;
        _scribbles = scribbles;
        _buildContext = buildContext;
        _setResult = setResult;
        _setStatus = setStatus;
        _onPreviewCompleted = onPreviewCompleted;
        _canRequestPreview = canRequestPreview;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await RunPreviewAsync();
        };
    }

    /// <summary>Restarts the debounce timer, coalescing rapid parameter changes into one preview run.</summary>
    public void RequestPreviewDebounced()
    {
        if (_canRequestPreview is not null && !_canRequestPreview())
        {
            return;
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>
    /// Runs the selected strategy at preview resolution and renders the result. Snapshotting the
    /// scribble masks and the preview Mat happens on the caller's (UI) thread: background runs must
    /// never touch the live Mats, which the UI disposes on stroke/undo/clear or when a new image loads.
    /// </summary>
    public async Task RunPreviewAsync()
    {
        var strategyKind = _selectedStrategy();
        var preview = _preview();
        if (preview is null || !_strategies.TryGetValue(strategyKind, out var strategy) || !_isReady(strategyKind))
        {
            return;
        }

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        try
        {
            var scribbles = _scribbles();
            using var fgScribble = scribbles.SnapshotForegroundScribble();
            using var bgScribble = scribbles.SnapshotBackgroundScribble();
            var context = _buildContext(fgScribble, bgScribble);

            using var previewBgr = preview.Bgr.Clone();
            var result = await strategy.RunPreviewAsync(previewBgr, context, cts.Token);

            if (cts.IsCancellationRequested)
            {
                result.Dispose();
                return;
            }

            _lastPreviewResult?.Dispose();
            _lastPreviewResult = result;
            _setResult(result.Bgra.ToBitmapSource());
            _onPreviewCompleted();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer preview request
        }
        catch (Exception ex)
        {
            _setStatus($"Preview failed: {ex.Message}");
        }
    }

    /// <summary>Cancels any in-flight preview run (used before loading a new image or project).</summary>
    public void CancelInFlight() => _previewCts?.Cancel();

    public void Dispose()
    {
        _debounceTimer.Stop();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _lastPreviewResult?.Dispose();
    }
}
