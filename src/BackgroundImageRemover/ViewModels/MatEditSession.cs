using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>
/// Owns the <see cref="EditHistory"/> for a hand-edited Mat (typically a working alpha channel)
/// and centralizes the record / undo / redo dispose-and-swap dance so callers stop hand-rolling it.
/// The Mat itself stays caller-owned (passed by reference on undo/redo).
/// </summary>
public sealed class MatEditSession : IDisposable
{
    private readonly EditHistory _editHistory = new();

    public bool CanUndo => _editHistory.CanUndo;
    public bool CanRedo => _editHistory.CanRedo;

    /// <summary>Records a snapshot of the current state before it is mutated further.</summary>
    public void Record(Mat? state)
    {
        if (state is not null)
        {
            _editHistory.Push(state);
        }
    }

    /// <summary>
    /// Swaps <paramref name="current"/> for the previous recorded state (disposing the old Mat).
    /// Returns false when there is nothing to undo.
    /// </summary>
    public bool Undo(ref Mat? current)
    {
        if (current is null)
        {
            return false;
        }
        var restored = _editHistory.Undo(current);
        if (restored is null)
        {
            return false;
        }
        current.Dispose();
        current = restored;
        return true;
    }

    /// <summary>
    /// Swaps <paramref name="current"/> for the previously undone state (disposing the old Mat).
    /// Returns false when there is nothing to redo.
    /// </summary>
    public bool Redo(ref Mat? current)
    {
        if (current is null)
        {
            return false;
        }
        var restored = _editHistory.Redo(current);
        if (restored is null)
        {
            return false;
        }
        current.Dispose();
        current = restored;
        return true;
    }

    public void Clear() => _editHistory.Clear();

    public void Dispose() => _editHistory.Dispose();
}
