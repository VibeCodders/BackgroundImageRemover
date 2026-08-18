using System.Windows.Threading;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Manages debounced preview operations to reduce code duplication and improve performance.
/// Handles the common pattern of debouncing preview requests with cancellation token management.
/// </summary>
public class PreviewDebouncer : IDisposable
{
    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _cts;
    private readonly Func<CancellationToken, Task> _previewAction;
    private readonly TimeSpan _debounceInterval;

    /// <summary>
    /// Creates a new preview debouncer.
    /// </summary>
    /// <param name="previewAction">The async action to execute for preview generation</param>
    /// <param name="debounceIntervalMs">Debounce interval in milliseconds (default: 150ms)</param>
    public PreviewDebouncer(Func<CancellationToken, Task> previewAction, int debounceIntervalMs = 150)
    {
        _previewAction = previewAction ?? throw new ArgumentNullException(nameof(previewAction));
        _debounceInterval = TimeSpan.FromMilliseconds(debounceIntervalMs);
        _debounceTimer = new DispatcherTimer { Interval = _debounceInterval };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await RunPreviewAsync();
        };
    }

    /// <summary>
    /// Requests a debounced preview execution.
    /// </summary>
    public void RequestPreview()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>
    /// Cancels any pending preview operation.
    /// </summary>
    public void CancelPending()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Immediately runs the preview without debouncing (cancels any pending operation first).
    /// </summary>
    public async Task RunImmediateAsync()
    {
        CancelPending();
        await RunPreviewAsync();
    }

    private async Task RunPreviewAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            await _previewAction(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer preview request - this is expected
        }
        catch (Exception)
        {
            // Let the caller handle other exceptions
            throw;
        }
        finally
        {
            if (_cts == cts)
            {
                _cts = null;
            }
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}