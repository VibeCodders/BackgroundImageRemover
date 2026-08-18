using System.Windows.Input;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Single-letter keyboard shortcuts that activate the editing tools while the image preview
/// has keyboard focus (no modifier keys). The letters follow each tool's English mnemonic;
/// W is special: it switches the removal strategy to the Magic Wand instead of opening a tab.
/// </summary>
public static class PreviewToolShortcuts
{
    public static readonly IReadOnlyDictionary<Key, EditorTool> ToolKeys = new Dictionary<Key, EditorTool>
    {
        [Key.R] = EditorTool.RemoveBackground, // Remove background
        [Key.U] = EditorTool.Uncrop,
        [Key.B] = EditorTool.Retouch,          // Brush
        [Key.A] = EditorTool.Adjustments,
        [Key.F] = EditorTool.Filters,
        [Key.T] = EditorTool.Transform,
        [Key.C] = EditorTool.Compose,
        [Key.G] = EditorTool.Frame,
        [Key.X] = EditorTool.Text,
        [Key.E] = EditorTool.Crop,
        [Key.S] = EditorTool.Resize,
        [Key.M] = EditorTool.Mosaic,
        [Key.O] = EditorTool.Overlay,
        [Key.L] = EditorTool.Levels,
        [Key.H] = EditorTool.Heal,
        [Key.J] = EditorTool.Liquify,
        [Key.P] = EditorTool.Perspective,
        [Key.K] = EditorTool.Fx,
        [Key.I] = EditorTool.TiltShift
    };

    /// <summary>W selects the Magic Wand removal strategy (it is not a modal tool tab).</summary>
    public static bool IsMagicWandKey(Key key) => key == Key.W;

    /// <summary>Returns the tool mapped to the key, or null when the key activates nothing.</summary>
    public static EditorTool? ToolForKey(Key key)
        => ToolKeys.TryGetValue(key, out var tool) ? tool : null;
}
