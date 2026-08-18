using OpenCvSharp;

namespace BackgroundImageRemover.Models;

/// <summary>A named target output size (in pixels) for an Uncrop operation.
/// <see cref="Size"/> is null for the free-form \"None\" preset.</summary>
public readonly record struct UncropSizePreset(string Label, Size? Size);

public static class UncropSizePresets
{
    public static readonly UncropSizePreset None = new("None", null);

    public static IReadOnlyList<UncropSizePreset> All { get; } =
    [
        None,
        new UncropSizePreset("Square 1024", new Size(1024, 1024)),
        new UncropSizePreset("Square 2048", new Size(2048, 2048)),
        new UncropSizePreset("HD 1920×1080", new Size(1920, 1080)),
        new UncropSizePreset("4K 3840×2160", new Size(3840, 2160)),
        new UncropSizePreset("Instagram 1080×1350", new Size(1080, 1350))
    ];
}
