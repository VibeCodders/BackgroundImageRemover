using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Base for tool sessions that work on an independent mutable BGR copy of the document
/// (<see cref="_workingBgr"/>) and build their result by chaining effect operations on it
/// (Heal, Retouch). Hosts the shared preview/apply template: subclasses implement
/// <see cref="BuildResult"/> once, and both the preview refresh and the final result use it.
/// </summary>
public abstract partial class WorkingCopyToolSessionViewModelBase : ToolSessionViewModelBase
{
    /// <summary>The independent BGR working copy of the source. Set by subclasses during init.</summary>
    protected Mat? _workingBgr;

    protected WorkingCopyToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
    }

    /// <summary>Builds the final BGR result from the working copy. Used for both preview and apply.</summary>
    protected abstract Mat BuildResult();

    /// <summary>Routes any <see cref="ToolParameterAttribute"/> change into the debounced preview refresh.</summary>
    protected override void OnToolParameterChanged() => RequestRefresh();

    /// <summary>Refreshes the preview bitmap from the working copy synchronously. Used by
    /// programmatic refresh points and tests; slider/parameter changes route through
    /// <see cref="ToolSessionViewModelBase.RequestRefresh"/> (debounced, see <see cref="RefreshAsync"/>).</summary>
    protected void RefreshResult()
    {
        if (_workingBgr is null || _workingAlpha is null)
        {
            return;
        }

        using var result = BuildResult();
        ResultBitmap = result.ToResultBitmap(_workingAlpha);
    }

    /// <summary>Debounced refresh: coalesces slider ticks into a single run of <see cref="RefreshResult"/>.</summary>
    protected override Task RefreshAsync()
    {
        RefreshResult();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgr?.Dispose();
        base.Dispose();
    }
}
