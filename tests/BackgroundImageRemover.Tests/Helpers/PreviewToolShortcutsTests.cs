using System.Windows.Input;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Tests.Helpers;

public class PreviewToolShortcutsTests
{
    [Theory]
    [InlineData(Key.B, EditorTool.Retouch)]   // brush
    [InlineData(Key.M, EditorTool.Mosaic)]
    [InlineData(Key.R, EditorTool.RemoveBackground)]
    [InlineData(Key.U, EditorTool.Uncrop)]
    [InlineData(Key.A, EditorTool.Adjustments)]
    [InlineData(Key.F, EditorTool.Filters)]
    [InlineData(Key.T, EditorTool.Transform)]
    [InlineData(Key.C, EditorTool.Compose)]
    [InlineData(Key.G, EditorTool.Frame)]
    [InlineData(Key.X, EditorTool.Text)]
    [InlineData(Key.E, EditorTool.Crop)]
    [InlineData(Key.S, EditorTool.Resize)]
    [InlineData(Key.O, EditorTool.Overlay)]
    [InlineData(Key.L, EditorTool.Levels)]
    [InlineData(Key.H, EditorTool.Heal)]
    [InlineData(Key.J, EditorTool.Liquify)]
    [InlineData(Key.P, EditorTool.Perspective)]
    [InlineData(Key.K, EditorTool.Fx)]
    [InlineData(Key.I, EditorTool.TiltShift)]
    public void ToolForKey_MapsLetterToTool(Key key, EditorTool expected)
    {
        Assert.Equal(expected, PreviewToolShortcuts.ToolForKey(key));
    }

    [Theory]
    [InlineData(Key.W, true)]
    [InlineData(Key.B, false)]
    [InlineData(Key.F5, false)]
    public void IsMagicWandKey_OnlyMatchesW(Key key, bool expected)
    {
        Assert.Equal(expected, PreviewToolShortcuts.IsMagicWandKey(key));
    }

    [Fact]
    public void ToolForKey_UnmappedKey_ReturnsNull()
    {
        Assert.Null(PreviewToolShortcuts.ToolForKey(Key.F5));
        Assert.Null(PreviewToolShortcuts.ToolForKey(Key.Enter));
    }

    [Fact]
    public void ToolKeys_AreUniquePerTool()
    {
        var tools = PreviewToolShortcuts.ToolKeys.Values;
        Assert.Equal(tools.Distinct().Count(), tools.Count());
        Assert.DoesNotContain(EditorTool.None, tools);
    }
}
