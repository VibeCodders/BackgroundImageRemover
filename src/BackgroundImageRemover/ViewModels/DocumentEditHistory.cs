using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.ViewModels;

/// <summary>A single named step in the document's undo/redo timeline.</summary>
public sealed record EditHistoryStep(string Name, bool IsUndone);

/// <summary>
/// Bounded undo/redo history for a document's working result. Snapshots store the full BGRA
/// state (color + alpha) together with a human-readable operation name, so the UI can show a
/// step-by-step timeline and undo any tool edit.
/// </summary>
public sealed class DocumentEditHistory : IDisposable
{
    private const int MaxDepth = 15;

    private readonly Stack<Entry> _undo = new();
    private readonly Stack<Entry> _redo = new();

    private sealed record Entry(string Name, Mat Bgra);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Raised whenever the timeline changes (record, undo, redo or clear).</summary>
    public event EventHandler? Changed;

    /// <summary>Records a snapshot of the current state before it is mutated further.</summary>
    public void Record(string name, Mat bgr, Mat alpha)
    {
        using var bgra = bgr.ToBgra(alpha);
        _undo.Push(new Entry(name, bgra.Clone()));

        while (_undo.Count > MaxDepth)
        {
            _undo.Pop().Bgra.Dispose();
        }

        foreach (var entry in _redo)
        {
            entry.Bgra.Dispose();
        }
        _redo.Clear();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Swaps the current BGR/alpha for the previous recorded state (disposing the old Mats).
    /// Returns false when there is nothing to undo.
    /// </summary>
    public bool Undo(ref Mat? bgr, ref Mat? alpha, out string name)
    {
        name = string.Empty;
        if (bgr is null || alpha is null || _undo.Count == 0)
        {
            return false;
        }

        using var current = bgr.ToBgra(alpha);
        var entry = _undo.Pop();
        _redo.Push(new Entry(entry.Name, current.Clone()));

        bgr.Dispose();
        alpha.Dispose();
        bgr = entry.Bgra.ToBgr();
        alpha = entry.Bgra.ExtractAlphaChannel();
        entry.Bgra.Dispose();
        name = entry.Name;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Swaps the current BGR/alpha for the previously undone state (disposing the old Mats).
    /// Returns false when there is nothing to redo.
    /// </summary>
    public bool Redo(ref Mat? bgr, ref Mat? alpha, out string name)
    {
        name = string.Empty;
        if (bgr is null || alpha is null || _redo.Count == 0)
        {
            return false;
        }

        using var current = bgr.ToBgra(alpha);
        var entry = _redo.Pop();
        _undo.Push(new Entry(entry.Name, current.Clone()));

        bgr.Dispose();
        alpha.Dispose();
        bgr = entry.Bgra.ToBgr();
        alpha = entry.Bgra.ExtractAlphaChannel();
        entry.Bgra.Dispose();
        name = entry.Name;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Builds a chronological timeline: applied steps first (oldest to newest), then the
    /// steps that were undone (in the order they will be redone).
    /// </summary>
    public IReadOnlyList<EditHistoryStep> BuildSteps()
    {
        var steps = new List<EditHistoryStep>(_undo.Count + _redo.Count);
        foreach (var entry in _undo.Reverse())
        {
            steps.Add(new EditHistoryStep(entry.Name, IsUndone: false));
        }
        foreach (var entry in _redo)
        {
            steps.Add(new EditHistoryStep(entry.Name, IsUndone: true));
        }
        return steps;
    }

    /// <summary>
    /// Jumps directly to the timeline step at <paramref name="chronologicalIndex"/> (the index
    /// used by <see cref="BuildSteps"/>: 0 = oldest). Steps before it are undone, steps after
    /// it are redone, so the working state matches that point in the edit session.
    /// Returns false when the index is out of range or no state is loaded.
    /// </summary>
    public bool RestoreTo(int chronologicalIndex, ref Mat? bgr, ref Mat? alpha, out string name)
    {
        name = string.Empty;
        if (bgr is null || alpha is null || chronologicalIndex < 0 || chronologicalIndex >= _undo.Count + _redo.Count)
        {
            return false;
        }

        int undos = _undo.Count - chronologicalIndex;
        if (undos < 0)
        {
            // The target is among the undone steps: redo it back into place.
            int redos = chronologicalIndex - _undo.Count + 1;
            for (int i = 0; i < redos; i++)
            {
                if (!Redo(ref bgr, ref alpha, out name))
                {
                    return false;
                }
            }
        }
        else
        {
            for (int i = 0; i < undos; i++)
            {
                if (!Undo(ref bgr, ref alpha, out name))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void Clear()
    {
        ClearEntries();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => ClearEntries();

    private void ClearEntries()
    {
        foreach (var entry in _undo)
        {
            entry.Bgra.Dispose();
        }
        foreach (var entry in _redo)
        {
            entry.Bgra.Dispose();
        }
        _undo.Clear();
        _redo.Clear();
    }
}
