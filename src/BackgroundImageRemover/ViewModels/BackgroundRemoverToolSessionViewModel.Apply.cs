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

        bool succeeded = await _fullRes.RunAsync(
            strategy,
            busyMessage: "Computing full-resolution background removal...",
            cancelledStatus: "Apply cancelled.",
            failureStatusPrefix: "Apply failed",
            onFailure: ex => _log.Error("Failed to apply background removal", ex),
            handleResult: result =>
            {
                ApplyBgra(result.Bgra, $"Remove Background ({SelectedStrategy})");
                result.Dispose();
                return true;
            });

        if (succeeded)
        {
            _shell.CloseTabDirect(this);
        }
    }
}
