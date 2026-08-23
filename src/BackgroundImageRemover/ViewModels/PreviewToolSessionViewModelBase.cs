using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Common base for single-effect tool tabs that render a live preview and apply a single
/// BGR-in/BGR-out transformation to the source image. Captures the preview + apply boilerplate
/// that was duplicated across the Vignette, Tilt-Shift, FX, Emoji, Text, Filters and Levels tools.
/// </summary>
public abstract partial class PreviewToolSessionViewModelBase : ToolSessionViewModelBase
{
    protected PreviewToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument, string statusMessage)
        : base(shell, parentDocument)
    {
        StatusMessage = statusMessage;
        InitSourceAlpha();
    }

    /// <summary>Computes the resulting BGR image from the full-resolution source.</summary>
    protected abstract Mat ApplyEffect(Mat bgr);

    /// <summary>Routes any <see cref="ToolParameterAttribute"/> change into the debounced live preview.</summary>
    protected override void OnToolParameterChanged() => RequestRefresh();

    /// <summary>Name recorded in the document's edit history when the tool is applied.</summary>
    protected abstract string OperationName { get; }

    /// <summary>True when the current parameters produce a visible change (drives the dirty flag).</summary>
    protected virtual bool IsEffectActive => false;

    /// <summary>
    /// Restores the tool's default parameters via <see cref="OnResetDefaults"/> and then refreshes
    /// the preview. The shared refresh is guaranteed to run after the defaults are applied, so
    /// subclasses cannot forget to repaint the preview when the Reset command runs.
    /// </summary>
    protected override void OnReset()
    {
        OnResetDefaults();
        RefreshPreview();
    }

    /// <summary>Restores the tool's default parameter values. Called by the shared Reset command before the preview refresh.</summary>
    protected virtual void OnResetDefaults()
    {
    }

    /// <summary>
    /// Builds and displays the live preview synchronously. Used by programmatic refresh points
    /// (Reset, initialization) and by tests; slider/parameter changes route through
    /// <see cref="ToolSessionViewModelBase.RequestRefresh"/> instead, which debounces and runs the
    /// same <see cref="ApplyEffect"/> off the UI thread (see <see cref="RefreshAsync"/>). The
    /// preview and the final apply share the exact same <see cref="ApplyEffect"/> implementation,
    /// so there is no possibility of the two drifting out of sync.
    /// </summary>
    protected void RefreshPreview()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            return;
        }

        using var result = ApplyEffect(_sourceImage.FullBgr);
        ResultBitmap = result.ToResultBitmap(_workingAlpha);
        IsDirty = IsEffectActive;
    }

    /// <summary>
    /// Debounced asynchronous refresh: snapshots the inputs on the UI thread, runs
    /// <see cref="ApplyEffect"/> at full resolution off the UI thread (so dragging a slider never
    /// freezes the UI), then renders the frozen bitmap on the dispatcher. A newer request cancels
    /// the previous run, so the displayed preview always matches the last parameter values.
    /// </summary>
    protected override async Task RefreshAsync()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            return;
        }

        var token = BeginRefresh();
        using var sourceBgr = _sourceImage.FullBgr.Clone();
        using var sourceAlpha = _workingAlpha.Clone();
        try
        {
            using var result = await Task.Run(() => ApplyEffect(sourceBgr), token);
            token.ThrowIfCancellationRequested();
            ResultBitmap = result.ToResultBitmap(sourceAlpha);
            IsDirty = IsEffectActive;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh or a reset; keep the previous preview.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    /// <summary>Applies the current effect to the parent document and closes the tool tab.</summary>
    public override Task ApplyAsync()
    {
        Mat? result = null;
        if (_sourceImage is not null)
        {
            result = ApplyEffect(_sourceImage.FullBgr);
        }

        ApplyAndClose(result, OperationName);
        return Task.CompletedTask;
    }
}
