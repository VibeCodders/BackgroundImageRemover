using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

public partial class BackgroundRemoverToolSessionViewModel
{
    public override async Task ApplyAsync()
    {
        if (IsBusy)
        {
            // An apply is already in flight; ignore the duplicate to prevent overlapping
            // full-resolution runs that race on the shared strategy cache.
            return;
        }

        if (_sourceImage is null || _preview is null || !_strategies.TryGetValue(SelectedStrategy, out var strategy))
        {
            _shell.CloseTabDirect(this);
            return;
        }

        _processCts?.Cancel();
        var cts = new CancellationTokenSource();
        _processCts = cts;

        bool succeeded = false;
        try
        {
            IsBusy = true;
            BusyMessage = "Computing full-resolution background removal...";

            // Full-res scribble copies must stay alive for the whole background run. Declaring
            // them with "using var" inside the if would dispose them at the closing brace --
            // before RunFullAsync even starts -- which surfaced as "Apply failed: Cannot access
            // a disposed object". They are declared here, in the method scope, and disposed
            // after the await completes.
            using var fgFull = SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles
                ? ScribbleManager.ForegroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size())
                : null;
            using var bgFull = SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles
                ? ScribbleManager.BackgroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size())
                : null;
            var context = BuildContext(_preview.ScaleFactor, fgFull, bgFull);

            // Snapshot the source on the UI thread: the run happens on a worker and pressing
            // Esc/Cancel mid-apply closes the tab, which disposes _sourceImage -- the run must
            // not read it after that.
            using var fullBgr = _sourceImage.FullBgr.Clone();
            var fullResult = await strategy.RunFullAsync(fullBgr, context, cts.Token);
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(fullResult.Bgra);
            fullResult.Dispose();

            _parentDocument.ApplyToolResult(bgr, alpha, $"Remove Background ({SelectedStrategy})");
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer apply or the tab was closed mid-run: this is not a
            // failure, so it must not surface as "Apply failed" or log an error.
            StatusMessage = "Apply cancelled.";
        }
        catch (Exception ex)
        {
            _log.Error("Failed to apply background removal", ex);
            StatusMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            if (succeeded)
            {
                _shell.CloseTabDirect(this);
            }
        }
    }
}
