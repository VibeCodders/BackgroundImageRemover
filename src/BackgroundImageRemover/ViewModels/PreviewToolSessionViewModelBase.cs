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
    [ObservableProperty]
    private BitmapSource? _resultBitmap;

    protected PreviewToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument, string statusMessage)
        : base(shell, parentDocument)
    {
        StatusMessage = statusMessage;
        InitSourceAlpha();
    }

    /// <summary>Computes the resulting BGR image from the full-resolution source.</summary>
    protected abstract Mat ApplyEffect(Mat bgr);

    /// <summary>Name recorded in the document's edit history when the tool is applied.</summary>
    protected abstract string OperationName { get; }

    /// <summary>True when the current parameters produce a visible change (drives the dirty flag).</summary>
    protected virtual bool IsEffectActive => false;

    /// <summary>
    /// Builds and displays the live preview. Called by subclasses whenever a parameter changes.
    /// The preview and the final apply share the exact same <see cref="ApplyEffect"/> implementation,
    /// so there is no possibility of the two drifting out of sync.
    /// </summary>
    protected void RefreshPreview()
    {
        if (_sourceImage is null || _workingAlpha is null)
        {
            return;
        }

        using var result = ApplyEffect(_sourceImage.FullBgr);
        ResultBitmap = result.ToBitmapSource(_workingAlpha);
        IsDirty = IsEffectActive;
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
