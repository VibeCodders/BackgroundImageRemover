using System.Windows.Media.Imaging;
using BackgroundImageRemover.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Manages GrabCut scribble operations to reduce code duplication between ViewModels.
/// Handles foreground/background scribble mats, undo/redo history, and drawing operations.
/// </summary>
public class ScribbleManager : IDisposable
{
    private Mat? _fgScribble;
    private Mat? _bgScribble;
    private WpfPoint? _lastPoint;
    private readonly Stack<(Mat Fg, Mat Bg)> _undo = new();
    private readonly Stack<(Mat Fg, Mat Bg)> _redo = new();
    private readonly int _maxHistoryDepth;

    public event EventHandler? StrokeUndone;
    public event EventHandler? StrokeRedone;
    public event EventHandler? ScribblesCleared;

    public bool HasScribbles => HasNonEmptyScribbles();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Eraser stroke thickness, in image pixels (wider than the draw stroke so erasing is forgiving).</summary>
    public const int EraseThickness = 16;

    /// <summary>
    /// Maps an <see cref="InteractionMode"/> to the corresponding <see cref="ScribbleMode"/>,
    /// defaulting to foreground for non-background modes.
    /// </summary>
    public static ScribbleMode FromInteractionMode(InteractionMode mode) => mode switch
    {
        InteractionMode.ScribbleBackground or InteractionMode.EraseBackground => ScribbleMode.Background,
        _ => ScribbleMode.Foreground
    };

    /// <summary>True when the given interaction mode clears scribbles instead of painting them.</summary>
    public static bool IsEraseMode(InteractionMode mode) => mode is InteractionMode.EraseForeground or InteractionMode.EraseBackground;

    public ScribbleManager(int maxHistoryDepth = 20)
    {
        _maxHistoryDepth = maxHistoryDepth;
    }

    /// <summary>
    /// Initializes scribble mats with the given size if they don't exist.
    /// </summary>
    public void EnsureMats(Size size)
    {
        _fgScribble ??= new Mat(size, MatType.CV_8UC1, Scalar.All(0));
        _bgScribble ??= new Mat(size, MatType.CV_8UC1, Scalar.All(0));
    }

    /// <summary>
    /// Starts a new scribble stroke at the given point.
    /// </summary>
    public void StartStroke(WpfPoint point, ScribbleMode mode)
    {
        PushUndoSnapshot();
        _lastPoint = point;
        DrawSegment(point, point, mode);
    }

    /// <summary>
    /// Continues a scribble stroke to the given point.
    /// </summary>
    public void MoveStroke(WpfPoint point, ScribbleMode mode)
    {
        if (_lastPoint is not { } last)
        {
            return;
        }
        DrawSegment(last, point, mode);
        _lastPoint = point;
    }

    /// <summary>
    /// Ends the current scribble stroke.
    /// </summary>
    public void EndStroke()
    {
        _lastPoint = null;
    }

    /// <summary>
    /// Starts a new eraser stroke at the given point, clearing only the target mask.
    /// </summary>
    public void StartErase(WpfPoint point, ScribbleMode mode)
    {
        PushUndoSnapshot();
        _lastPoint = point;
        EraseSegment(point, point, mode);
    }

    /// <summary>
    /// Continues an eraser stroke to the given point.
    /// </summary>
    public void MoveErase(WpfPoint point, ScribbleMode mode)
    {
        if (_lastPoint is not { } last)
        {
            return;
        }
        EraseSegment(last, point, mode);
        _lastPoint = point;
    }

    /// <summary>
    /// Draws a scribble segment between two points.
    /// </summary>
    private void DrawSegment(WpfPoint from, WpfPoint to, ScribbleMode mode)
    {
        if (_fgScribble is null || _bgScribble is null)
        {
            return;
        }

        var p1 = new Point((int)Math.Round(from.X), (int)Math.Round(from.Y));
        var p2 = new Point((int)Math.Round(to.X), (int)Math.Round(to.Y));
        const int thickness = 6;

        if (mode == ScribbleMode.Foreground)
        {
            Cv2.Line(_fgScribble, p1, p2, Scalar.All(255), thickness, LineTypes.AntiAlias);
            Cv2.Line(_bgScribble, p1, p2, Scalar.All(0), thickness, LineTypes.AntiAlias);
        }
        else if (mode == ScribbleMode.Background)
        {
            Cv2.Line(_bgScribble, p1, p2, Scalar.All(255), thickness, LineTypes.AntiAlias);
            Cv2.Line(_fgScribble, p1, p2, Scalar.All(0), thickness, LineTypes.AntiAlias);
        }
    }

    /// <summary>
    /// Clears a scribble segment from the given mask only (the opposite mask is left untouched).
    /// </summary>
    private void EraseSegment(WpfPoint from, WpfPoint to, ScribbleMode mode)
    {
        if (_fgScribble is null || _bgScribble is null)
        {
            return;
        }

        var p1 = new Point((int)Math.Round(from.X), (int)Math.Round(from.Y));
        var p2 = new Point((int)Math.Round(to.X), (int)Math.Round(to.Y));
        var target = mode == ScribbleMode.Foreground ? _fgScribble : _bgScribble;

        Cv2.Line(target, p1, p2, Scalar.All(0), EraseThickness, LineTypes.AntiAlias);
    }

    /// <summary>
    /// Undoes the last scribble stroke.
    /// </summary>
    public bool Undo()
    {
        if (_undo.Count == 0 || _fgScribble is null || _bgScribble is null)
        {
            return false;
        }

        _redo.Push((_fgScribble.Clone(), _bgScribble.Clone()));
        var (fg, bg) = _undo.Pop();
        _fgScribble.Dispose();
        _bgScribble.Dispose();
        _fgScribble = fg;
        _bgScribble = bg;
        StrokeUndone?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Redoes the previously undone scribble stroke.
    /// </summary>
    public bool Redo()
    {
        if (_redo.Count == 0 || _fgScribble is null || _bgScribble is null)
        {
            return false;
        }

        _undo.Push((_fgScribble.Clone(), _bgScribble.Clone()));
        var (fg, bg) = _redo.Pop();
        _fgScribble.Dispose();
        _bgScribble.Dispose();
        _fgScribble = fg;
        _bgScribble = bg;
        StrokeRedone?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Clears all scribbles and history.
    /// </summary>
    public void Clear()
    {
        _fgScribble?.Dispose();
        _bgScribble?.Dispose();
        _fgScribble = null;
        _bgScribble = null;

        foreach (var (f, b) in _undo) { f.Dispose(); b.Dispose(); }
        foreach (var (f, b) in _redo) { f.Dispose(); b.Dispose(); }
        _undo.Clear();
        _redo.Clear();

        ScribblesCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the foreground scribble mat (read-only).
    /// </summary>
    /// <remarks>
    /// The returned Mat is owned by this manager and may be disposed at any time by the next
    /// stroke, undo/redo, clear or <see cref="Dispose"/>. Background threads must never hold
    /// this reference; use <see cref="SnapshotForegroundScribble"/> instead.
    /// </remarks>
    public Mat? ForegroundScribble => _fgScribble;

    /// <summary>
    /// Gets the background scribble mat (read-only).
    /// </summary>
    /// <remarks>
    /// The returned Mat is owned by this manager and may be disposed at any time by the next
    /// stroke, undo/redo, clear or <see cref="Dispose"/>. Background threads must never hold
    /// this reference; use <see cref="SnapshotBackgroundScribble"/> instead.
    /// </remarks>
    public Mat? BackgroundScribble => _bgScribble;

    /// <summary>
    /// Returns a private clone of the current foreground scribble mask, or null when there is
    /// none. The caller owns the clone and it stays valid even if the manager later clears,
    /// undoes or redraws the live masks -- preview/apply runs on background threads must use
    /// these snapshots, never <see cref="ForegroundScribble"/>.
    /// </summary>
    public Mat? SnapshotForegroundScribble() => _fgScribble?.Clone();

    /// <summary>
    /// Returns a private clone of the current background scribble mask, or null when there is
    /// none. The caller owns the clone and it stays valid even if the manager later clears,
    /// undoes or redraws the live masks -- preview/apply runs on background threads must use
    /// these snapshots, never <see cref="BackgroundScribble"/>.
    /// </summary>
    public Mat? SnapshotBackgroundScribble() => _bgScribble?.Clone();

    /// <summary>
    /// Renders the current scribbles as a semi-transparent overlay bitmap (green = foreground,
    /// red = background), sized to the scribble mats so it can be drawn directly over the preview.
    /// </summary>
    public BitmapSource? BuildOverlayBitmap()
    {
        if (_fgScribble is null && _bgScribble is null)
        {
            return null;
        }

        var size = (_fgScribble ?? _bgScribble)!.Size();
        using var overlay = new Mat(size, MatType.CV_8UC4, Scalar.All(0));
        if (_fgScribble is not null)
        {
            overlay.SetTo(new Scalar(50, 205, 50, 190), _fgScribble);
        }
        if (_bgScribble is not null)
        {
            overlay.SetTo(new Scalar(0, 0, 255, 190), _bgScribble);
        }
        return overlay.ToBitmapSource();
    }

    private void PushUndoSnapshot()
    {
        if (_fgScribble is null || _bgScribble is null)
        {
            return;
        }

        _undo.Push((_fgScribble.Clone(), _bgScribble.Clone()));
        _undo.TrimStack(_maxHistoryDepth, drop =>
        {
            drop.Fg.Dispose();
            drop.Bg.Dispose();
        });

        foreach (var (f, b) in _redo) { f.Dispose(); b.Dispose(); }
        _redo.Clear();
    }

    private bool HasNonEmptyScribbles()
    {
        return (_fgScribble is not null && Cv2.CountNonZero(_fgScribble) > 0)
            || (_bgScribble is not null && Cv2.CountNonZero(_bgScribble) > 0);
    }

    public void Dispose()
    {
        Clear();
        _fgScribble?.Dispose();
        _bgScribble?.Dispose();
    }
}

/// <summary>
/// Mode for scribble drawing operations.
/// </summary>
public enum ScribbleMode
{
    Foreground,
    Background
}