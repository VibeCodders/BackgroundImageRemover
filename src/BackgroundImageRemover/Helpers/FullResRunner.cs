using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Shared full-resolution strategy-run orchestration for the inline document editor
/// (<see cref="ViewModels.DocumentViewModel"/>, which feeds the working result into its export
/// pipeline) and the dedicated Background Remover tool tab (<see cref="ViewModels.BackgroundRemoverToolSessionViewModel"/>,
/// which applies the result to the parent document and closes). Both hosts previously duplicated
/// the full-res scribble snapshotting, the preview-scaled context building, the source snapshot,
/// the cancellation-token lifecycle and the busy/error envelope; the host state (source image,
/// preview, selected strategy, scribble manager, context builder, busy/status setters) is
/// injected so behavior stays identical in both hosts.
/// </summary>
public sealed class FullResRunner : IDisposable
{
    private readonly Func<LoadedImage?> _source;
    private readonly Func<PreviewImage?> _preview;
    private readonly Func<StrategyKind> _selectedStrategy;
    private readonly Func<ScribbleManager> _scribbles;
    private readonly Func<double, Mat?, Mat?, StrategyContext> _buildContext;
    private readonly Action<bool> _setBusy;
    private readonly Action<string> _setBusyMessage;
    private readonly Action<string> _setStatus;

    private CancellationTokenSource? _processCts;

    /// <param name="source">Provides the full-resolution source image (the document's loaded image
    /// or the tool session's snapshot).</param>
    /// <param name="preview">Provides the preview image, whose scale factor is used to build the context.</param>
    /// <param name="selectedStrategy">Provides the currently selected <see cref="StrategyKind"/>.</param>
    /// <param name="scribbles">Provides the host's <see cref="ScribbleManager"/> for full-res snapshots.</param>
    /// <param name="buildContext">Builds the <see cref="StrategyContext"/> from the preview scale factor and the
    /// foreground/background scribble snapshots plus the host's seed state.</param>
    /// <param name="setBusy">Flips the host's busy flag around the run.</param>
    /// <param name="setBusyMessage">Shows the busy overlay message during the run.</param>
    /// <param name="setStatus">Reports status/error messages to the host.</param>
    public FullResRunner(
        Func<LoadedImage?> source,
        Func<PreviewImage?> preview,
        Func<StrategyKind> selectedStrategy,
        Func<ScribbleManager> scribbles,
        Func<double, Mat?, Mat?, StrategyContext> buildContext,
        Action<bool> setBusy,
        Action<string> setBusyMessage,
        Action<string> setStatus)
    {
        _source = source;
        _preview = preview;
        _selectedStrategy = selectedStrategy;
        _scribbles = scribbles;
        _buildContext = buildContext;
        _setBusy = setBusy;
        _setBusyMessage = setBusyMessage;
        _setStatus = setStatus;
    }

    /// <summary>
    /// Runs <paramref name="strategy"/> at full resolution inside the shared busy/CTS/error
    /// envelope, then hands the result to <paramref name="handleResult"/> (which takes ownership
    /// of the result: the document stores it in its working pair, the tool tab applies it to the
    /// parent). Returns true when the run succeeded and the result was handled.
    /// </summary>
    /// <param name="strategy">The resolved strategy to run (the host guards availability first).</param>
    /// <param name="busyMessage">Busy overlay text while the run is in flight.</param>
    /// <param name="cancelledStatus">Status shown when the run was superseded or cancelled.</param>
    /// <param name="failureStatusPrefix">Prefix of the failure status, followed by ": {message}".</param>
    /// <param name="onFailure">Logs the failure (the message text is host-specific).</param>
    /// <param name="handleResult">Consumes the result (must dispose it) and returns success.</param>
    public async Task<bool> RunAsync(
        IBackgroundRemovalStrategy strategy,
        string busyMessage,
        string cancelledStatus,
        string failureStatusPrefix,
        Action<Exception> onFailure,
        Func<RemovalResult, bool> handleResult)
    {
        _processCts?.Cancel();
        var cts = new CancellationTokenSource();
        _processCts = cts;

        try
        {
            _setBusy(true);
            _setBusyMessage(busyMessage);

            var result = await RunFullAsync(strategy, cts.Token);
            return handleResult(result);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer run or the tab was closed mid-run: not a failure, so it must
            // not surface as a failure status or be logged as an error.
            _setStatus(cancelledStatus);
            return false;
        }
        catch (Exception ex)
        {
            onFailure(ex);
            _setStatus($"{failureStatusPrefix}: {ex.Message}");
            return false;
        }
        finally
        {
            _setBusy(false);
        }
    }

    /// <summary>
    /// Runs the strategy at full resolution with the same parameters the preview uses. Full-res
    /// scribble copies and the source snapshot are taken on the caller's (UI) thread and stay
    /// alive for the whole run: background runs must never touch the live Mats, which the UI
    /// disposes when a new image loads, undo/redo runs or the tab closes.
    /// </summary>
    private async Task<RemovalResult> RunFullAsync(IBackgroundRemovalStrategy strategy, CancellationToken ct)
    {
        var source = _source();
        var preview = _preview();
        if (source is null || preview is null)
        {
            throw new InvalidOperationException("No image loaded.");
        }

        var kind = _selectedStrategy();
        var scribbles = _scribbles();
        using var fgFull = kind == StrategyKind.GrabCut && scribbles.HasScribbles
            ? scribbles.ForegroundScribble?.ResizeScribble(source.FullBgr.Size())
            : null;
        using var bgFull = kind == StrategyKind.GrabCut && scribbles.HasScribbles
            ? scribbles.BackgroundScribble?.ResizeScribble(source.FullBgr.Size())
            : null;
        var context = _buildContext(preview.ScaleFactor, fgFull, bgFull);

        using var fullBgr = source.FullBgr.Clone();
        return await strategy.RunFullAsync(fullBgr, context, ct);
    }

    public void Dispose()
    {
        _processCts?.Cancel();
        _processCts?.Dispose();
    }
}
