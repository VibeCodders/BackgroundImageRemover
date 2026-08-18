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

            var context = BuildContext(_preview.ScaleFactor);
            if (SelectedStrategy == StrategyKind.GrabCut && ScribbleManager.HasScribbles)
            {
                using var fgFull = ScribbleManager.ForegroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size());
                using var bgFull = ScribbleManager.BackgroundScribble?.ResizeScribble(_sourceImage.FullBgr.Size());
                context = context with { GrabCutForegroundScribble = fgFull, GrabCutBackgroundScribble = bgFull };
            }

            var fullResult = await strategy.RunFullAsync(_sourceImage.FullBgr, context, cts.Token);
            var (bgr, alpha) = BackgroundCompositingService.SplitBgra(fullResult.Bgra);
            fullResult.Dispose();

            _parentDocument.ApplyToolResult(bgr, alpha, $"Remove Background ({SelectedStrategy})");
            succeeded = true;
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
