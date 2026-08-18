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
