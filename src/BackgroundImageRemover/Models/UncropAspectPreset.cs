namespace BackgroundImageRemover.Models;

/// <summary>A named target aspect ratio (width/height) offered as a one-click Uncrop preset.
/// <see cref="Ratio"/> is null for "Free" (unconstrained) and "Custom" (padding hand-edited).</summary>
public readonly record struct UncropAspectPreset(string Label, double? Ratio);

public static class UncropAspectPresets
{
    /// <summary>No target ratio: handles and numeric fields move independently.</summary>
    public static readonly UncropAspectPreset Free = new("Free", null);

    /// <summary>Selected automatically once the user hand-edits padding away from a chosen preset.</summary>
    public static readonly UncropAspectPreset Custom = new("Custom", null);

    public static IReadOnlyList<UncropAspectPreset> All { get; } =
    [
        Free,
        new UncropAspectPreset("Square 1:1", 1.0),
        new UncropAspectPreset("16:9", 16.0 / 9.0),
        new UncropAspectPreset("9:16", 9.0 / 16.0),
        new UncropAspectPreset("4:5", 4.0 / 5.0),
        new UncropAspectPreset("5:4", 5.0 / 4.0),
        new UncropAspectPreset("3:2", 3.0 / 2.0),
        new UncropAspectPreset("2:3", 2.0 / 3.0)
    ];
}
