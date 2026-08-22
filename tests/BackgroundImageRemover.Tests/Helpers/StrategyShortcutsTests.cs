using System.Windows.Input;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Tests.Helpers;

public class StrategyShortcutsTests
{
    [Theory]
    [InlineData(Key.D1, StrategyKind.ChromaKey)]
    [InlineData(Key.D2, StrategyKind.GrabCut)]
    [InlineData(Key.D3, StrategyKind.Onnx)]
    [InlineData(Key.D4, StrategyKind.Sam)]
    [InlineData(Key.D5, StrategyKind.FloodFill)]
    [InlineData(Key.D6, StrategyKind.KMeans)]
    [InlineData(Key.D7, StrategyKind.Otsu)]
    [InlineData(Key.D8, StrategyKind.MagicWand)]
    [InlineData(Key.D0, StrategyKind.EdgeContour)]
    public void StrategyForKey_MapsEachDigitToItsStrategy(Key key, StrategyKind expected)
    {
        Assert.Equal(expected, StrategyShortcuts.StrategyForKey(key));
    }

    [Theory]
    [InlineData(Key.NumPad1, StrategyKind.ChromaKey)]
    [InlineData(Key.NumPad3, StrategyKind.Onnx)]
    [InlineData(Key.NumPad8, StrategyKind.MagicWand)]
    public void StrategyForKey_SupportsNumpadKeys(Key key, StrategyKind expected)
    {
        Assert.Equal(expected, StrategyShortcuts.StrategyForKey(key));
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Escape)]
    public void StrategyForKey_ReturnsNullForUnmappedKeys(Key key)
    {
        Assert.Null(StrategyShortcuts.StrategyForKey(key));
    }

    [Fact]
    public void StrategyForKey_CoversEveryStrategyExactlyOnce()
    {
        var mapped = StrategyShortcuts.StrategyKeys.Values.Distinct().ToList();

        Assert.Equal(Enum.GetValues<StrategyKind>().Length, mapped.Count);
        Assert.Equal(Enum.GetValues<StrategyKind>().OrderBy(x => x), mapped.OrderBy(x => x));
    }
}
