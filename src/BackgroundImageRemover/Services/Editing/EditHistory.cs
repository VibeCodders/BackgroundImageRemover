using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Bounded undo/redo stack of Mat snapshots (e.g. an alpha channel being hand-edited).
/// Snapshots are cloned on push so the caller's live Mat can keep mutating in place.
/// </summary>
public sealed class EditHistory : IDisposable
{
    private const int MaxDepth = 20;

    private readonly Stack<Mat> _undo = new();
    private readonly Stack<Mat> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records the given state as the new undo point, before it's mutated further.</summary>
    public void Push(Mat state)
    {
        _undo.Push(state.Clone());
        while (_undo.Count > MaxDepth)
        {
            // Drop the oldest entry; Stack doesn't support removing from the bottom directly,
            // so rebuild via a temporary array (small, bounded, infrequent).
            // Stack.ToArray() returns elements from top (newest) to bottom (oldest),
            // so items[0] is newest and items[^1] is oldest.
            var items = _undo.ToArray();
            _undo.Clear();
            for (int i = MaxDepth - 1; i >= 0; i--)
            {
                _undo.Push(items[i]);
            }
            items[^1].Dispose();
        }

        foreach (var redoState in _redo)
        {
            redoState.Dispose();
        }
        _redo.Clear();
    }

    /// <summary>Pops the last recorded state, pushing <paramref name="current"/> onto the redo stack.</summary>
    public Mat? Undo(Mat current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }
        _redo.Push(current.Clone());
        return _undo.Pop();
    }

    /// <summary>Pops the last undone state, pushing <paramref name="current"/> back onto the undo stack.</summary>
    public Mat? Redo(Mat current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }
        _undo.Push(current.Clone());
        return _redo.Pop();
    }

    public void Clear()
    {
        foreach (var state in _undo) state.Dispose();
        foreach (var state in _redo) state.Dispose();
        _undo.Clear();
        _redo.Clear();
    }

    public void Dispose() => Clear();
}
