using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Bounded undo/redo history for a document's working result. Snapshots store the full BGRA
/// state so undoing a tool edit restores both the color and the alpha channel together.
/// </summary>
public sealed class DocumentEditHistory : IDisposable
{
    private readonly EditHistory _history = new(maxDepth: 15);

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Records a snapshot of the current state before it is mutated further.</summary>
    public void Record(Mat bgr, Mat alpha)
    {
        using var bgra = bgr.ToBgra(alpha);
        _history.Push(bgra);
    }

    /// <summary>
    /// Swaps the current BGR/alpha for the previous recorded state (disposing the old Mats).
    /// Returns false when there is nothing to undo.
    /// </summary>
    public bool Undo(ref Mat? bgr, ref Mat? alpha)
    {
        if (bgr is null || alpha is null)
        {
            return false;
        }

        using var current = bgr.ToBgra(alpha);
        var restored = _history.Undo(current);
        if (restored is null)
        {
            return false;
        }

        bgr.Dispose();
        alpha.Dispose();
        bgr = restored.ToBgr();
        alpha = restored.ExtractAlphaChannel();
        restored.Dispose();
        return true;
    }

    /// <summary>
    /// Swaps the current BGR/alpha for the previously undone state (disposing the old Mats).
    /// Returns false when there is nothing to redo.
    /// </summary>
    public bool Redo(ref Mat? bgr, ref Mat? alpha)
    {
        if (bgr is null || alpha is null)
        {
            return false;
        }

        using var current = bgr.ToBgra(alpha);
        var restored = _history.Redo(current);
        if (restored is null)
        {
            return false;
        }

        bgr.Dispose();
        alpha.Dispose();
        bgr = restored.ToBgr();
        alpha = restored.ExtractAlphaChannel();
        restored.Dispose();
        return true;
    }

    public void Clear() => _history.Clear();

    public void Dispose() => _history.Dispose();
}
