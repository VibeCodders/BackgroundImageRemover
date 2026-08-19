using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace BackgroundImageRemover.Tests.Helpers;

public class ScribbleManagerTests
{
    [Fact]
    public void FromInteractionMode_MapsEraserModesToTheirMask()
    {
        Assert.Equal(ScribbleMode.Foreground, ScribbleManager.FromInteractionMode(InteractionMode.EraseForeground));
        Assert.Equal(ScribbleMode.Background, ScribbleManager.FromInteractionMode(InteractionMode.EraseBackground));
    }

    [Fact]
    public void IsEraseMode_RecognizesOnlyEraserModes()
    {
        Assert.True(ScribbleManager.IsEraseMode(InteractionMode.EraseForeground));
        Assert.True(ScribbleManager.IsEraseMode(InteractionMode.EraseBackground));
        Assert.False(ScribbleManager.IsEraseMode(InteractionMode.ScribbleForeground));
        Assert.False(ScribbleManager.IsEraseMode(InteractionMode.ScribbleBackground));
        Assert.False(ScribbleManager.IsEraseMode(InteractionMode.Brush));
    }

    [Fact]
    public void EraseForeground_ClearsForegroundOnly()
    {
        using var manager = new ScribbleManager();
        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Foreground, xFrom: 5, xTo: 45, y: 10);

        Assert.Equal(255, manager.ForegroundScribble!.At<byte>(10, 25));

        manager.StartErase(new WpfPoint(20, 10), ScribbleMode.Foreground);
        manager.MoveErase(new WpfPoint(30, 10), ScribbleMode.Foreground);
        manager.EndStroke();

        Assert.Equal(0, manager.ForegroundScribble.At<byte>(10, 25));    // erased
        Assert.Equal(255, manager.ForegroundScribble.At<byte>(10, 7));   // outside the eraser swath
        Assert.Equal(0, manager.BackgroundScribble!.At<byte>(10, 25));   // background is untouched
    }

    [Fact]
    public void EraseBackground_ClearsBackgroundOnly()
    {
        using var manager = new ScribbleManager();
        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Background, xFrom: 5, xTo: 45, y: 10);

        Assert.Equal(255, manager.BackgroundScribble!.At<byte>(10, 25));

        manager.StartErase(new WpfPoint(20, 10), ScribbleMode.Background);
        manager.MoveErase(new WpfPoint(30, 10), ScribbleMode.Background);
        manager.EndStroke();

        Assert.Equal(0, manager.BackgroundScribble.At<byte>(10, 25));
        Assert.Equal(0, manager.ForegroundScribble!.At<byte>(10, 25));   // foreground is untouched
    }

    [Fact]
    public void Erase_IsUndoable()
    {
        using var manager = new ScribbleManager();
        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Foreground, xFrom: 5, xTo: 45, y: 10);

        manager.StartErase(new WpfPoint(20, 10), ScribbleMode.Foreground);
        manager.MoveErase(new WpfPoint(30, 10), ScribbleMode.Foreground);
        manager.EndStroke();
        Assert.Equal(0, manager.ForegroundScribble!.At<byte>(10, 25));

        Assert.True(manager.Undo());
        Assert.Equal(255, manager.ForegroundScribble.At<byte>(10, 25));
    }

    [Fact]
    public void Snapshot_ReturnsNullWhenNoScribblesExist()
    {
        using var manager = new ScribbleManager();
        Assert.Null(manager.SnapshotForegroundScribble());
        Assert.Null(manager.SnapshotBackgroundScribble());
    }

    [Fact]
    public void Snapshot_SurvivesManagerClear_AndIsIndependentOfTheLiveMask()
    {
        // Preview/apply runs on background threads must use snapshots, never the live Mats:
        // the UI thread disposes the live Mats on Clear/Undo/Redo mid-run. A snapshot taken
        // before Clear must stay fully usable afterwards (regression: "Cannot access a
        // disposed object" when a rect selection cleared the scribbles mid-preview).
        using var manager = new ScribbleManager();
        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Foreground, xFrom: 5, xTo: 45, y: 10);
        DrawHorizontal(manager, ScribbleMode.Background, xFrom: 5, xTo: 45, y: 40);

        using var fgSnapshot = manager.SnapshotForegroundScribble();
        using var bgSnapshot = manager.SnapshotBackgroundScribble();
        Assert.NotNull(fgSnapshot);
        Assert.NotNull(bgSnapshot);

        // The UI thread now clears (e.g. the user drew a new rectangle).
        manager.Clear();
        Assert.Null(manager.ForegroundScribble);

        // The snapshots still carry the scribble pixels and are safe to hand to OpenCV.
        Assert.True(Cv2.CountNonZero(fgSnapshot!) > 0);
        Assert.True(Cv2.CountNonZero(bgSnapshot!) > 0);
        using var cleared = new Mat();
        Cv2.Compare(fgSnapshot, new Scalar(255), cleared, CmpType.EQ); // any operation must not throw
    }

    [Fact]
    public void Snapshot_IsUnaffectedByUndoDisposingTheLiveMats()
    {
        using var manager = new ScribbleManager();
        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Foreground, xFrom: 5, xTo: 45, y: 10);

        using var snapshot = manager.SnapshotForegroundScribble();
        Assert.NotNull(snapshot);

        // Undo swaps in clones and disposes the live Mats; the snapshot must remain valid.
        manager.StartStroke(new WpfPoint(10, 10), ScribbleMode.Foreground);
        manager.MoveStroke(new WpfPoint(20, 10), ScribbleMode.Foreground);
        manager.EndStroke();
        Assert.True(manager.Undo());

        Assert.True(Cv2.CountNonZero(snapshot!) > 0);
    }

    [Fact]
    public void BuildOverlayBitmap_ReturnsNullWhenEmpty_AndBitmapAfterScribble()
    {
        using var manager = new ScribbleManager();
        Assert.Null(manager.BuildOverlayBitmap());

        manager.EnsureMats(new Size(60, 60));
        DrawHorizontal(manager, ScribbleMode.Foreground, xFrom: 5, xTo: 45, y: 10);

        var overlay = manager.BuildOverlayBitmap();
        Assert.NotNull(overlay);
        Assert.Equal(60, overlay.PixelWidth);
        Assert.Equal(60, overlay.PixelHeight);
    }

    private static void DrawHorizontal(ScribbleManager manager, ScribbleMode mode, int xFrom, int xTo, int y)
    {
        manager.StartStroke(new WpfPoint(xFrom, y), mode);
        manager.MoveStroke(new WpfPoint(xTo, y), mode);
        manager.EndStroke();
    }
}
