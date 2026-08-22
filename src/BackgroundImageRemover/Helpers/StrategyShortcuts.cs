using System.Windows.Input;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Number-key shortcuts (1-8, no modifiers) that switch the removal strategy while the
/// Background Remover tool session has keyboard focus. The mapping is explicit rather than
/// derived from enum order, so the digits stay stable and memorable across versions.
/// </summary>
public static class StrategyShortcuts
{
    public static readonly IReadOnlyDictionary<Key, StrategyKind> StrategyKeys = new Dictionary<Key, StrategyKind>
    {
        [Key.D1] = StrategyKind.ChromaKey,
        [Key.NumPad1] = StrategyKind.ChromaKey,
        [Key.D2] = StrategyKind.GrabCut,
        [Key.NumPad2] = StrategyKind.GrabCut,
        [Key.D3] = StrategyKind.Onnx,
        [Key.NumPad3] = StrategyKind.Onnx,
        [Key.D4] = StrategyKind.Sam,
        [Key.NumPad4] = StrategyKind.Sam,
        [Key.D5] = StrategyKind.FloodFill,
        [Key.NumPad5] = StrategyKind.FloodFill,
        [Key.D6] = StrategyKind.KMeans,
        [Key.NumPad6] = StrategyKind.KMeans,
        [Key.D7] = StrategyKind.Otsu,
        [Key.NumPad7] = StrategyKind.Otsu,
        [Key.D8] = StrategyKind.MagicWand,
        [Key.NumPad8] = StrategyKind.MagicWand,
        [Key.D9] = StrategyKind.Inpaint,
        [Key.NumPad9] = StrategyKind.Inpaint,
        [Key.D0] = StrategyKind.EdgeContour,
        [Key.NumPad0] = StrategyKind.EdgeContour
    };

    /// <summary>Returns the strategy mapped to the key, or null when the key switches nothing.</summary>
    public static StrategyKind? StrategyForKey(Key key)
        => StrategyKeys.TryGetValue(key, out var strategy) ? strategy : null;
}
