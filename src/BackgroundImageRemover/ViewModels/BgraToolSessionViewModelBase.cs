using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Base for tool sessions that work on a fused BGRA copy of the document (pixels + alpha
/// together), such as Transform, Liquify, Rotate, Crop and Lasso.
/// Encapsulates the snapshot → BGRA conversion, the <see cref="SourceBitmap"/> preview, the
/// <see cref="RefreshBgraPreview"/> helper, the split-and-apply lifecycle, and disposal of
/// the BGRA working copy — all the boilerplate that was duplicated across those tools.
/// </summary>
public abstract partial class BgraToolSessionViewModelBase : ToolSessionViewModelBase
{
    private Mat? _workingBgra;

    [ObservableProperty]
    private BitmapSource? _sourceBitmap;

    /// <summary>The fused BGRA working copy (pixels + alpha). Derived classes may replace it.</summary>
    protected Mat? WorkingBgra
    {
        get => _workingBgra;
        set => _workingBgra = value;
    }

    protected BgraToolSessionViewModelBase(ShellViewModel shell, DocumentViewModel parentDocument)
        : base(shell, parentDocument)
    {
    }

    /// <summary>Captures the document snapshot, fuses it into a BGRA copy and exposes the source bitmap.</summary>
    protected void InitWorkingBgra()
    {
        InitSourceAlpha();
        ResetWorkingBgra();
        SourceBitmap = _workingBgra!.ToFrozenBitmapSource();
    }

    /// <summary>
    /// Replaces the working BGRA copy with <paramref name="newBgra"/>, disposing the previous
    /// one. Takes ownership of <paramref name="newBgra"/> (the caller must not dispose it).
    /// Eliminates the copy-pasted dispose + reassign pair in the transform tools.
    /// </summary>
    protected void ReplaceWorkingBgra(Mat newBgra)
    {
        _workingBgra?.Dispose();
        _workingBgra = newBgra;
    }

    /// <summary>Replaces the working BGRA copy with a fresh fusion of the pristine source and its alpha.</summary>
    protected void ResetWorkingBgra()
    {
        ReplaceWorkingBgra(_sourceImage!.FullBgr.ToBgra(_workingAlpha!));
    }

    /// <summary>Updates <see cref="ToolSessionViewModelBase.ResultBitmap"/> from the current BGRA copy.</summary>
    protected void RefreshBgraPreview()
    {
        if (_workingBgra is not null)
        {
            ResultBitmap = _workingBgra.ToFrozenBitmapSource();
        }
    }

    /// <summary>
    /// Splits the given BGRA result, applies it to the parent document and closes the tab.
    /// Call from a custom <see cref="ApplyAsync"/> override when the result is not the
    /// working BGRA copy itself (e.g. after cropping or rotating).
    /// </summary>
    protected void ApplyBgraResult(Mat bgra, string operationName)
        => ApplyBgraAndClose(bgra, operationName);

    /// <summary>Applies the working BGRA copy directly (split into BGR + alpha) and closes the tab.</summary>
    protected Task ApplyWorkingBgraAsync(string operationName)
    {
        if (_workingBgra is not null)
        {
            ApplyBgra(_workingBgra, operationName);
        }
        _shell.CloseTabDirect(this);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _workingBgra?.Dispose();
        base.Dispose();
    }
}