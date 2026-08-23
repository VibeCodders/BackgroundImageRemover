namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Which LaMa checkpoint to run. <see cref="Large"/> is the default: the Carve/LaMa-ONNX port of
/// big-lama (fixed 512×512 input, inputs <c>image</c> [1,3,512,512] RGB 0-1 and <c>mask</c>
/// [1,1,512,512] 0/1, output [1,3,512,512] 0-255 with the original pixels already composited
/// back in the unmasked region). Small and Middle are faster-to-download / lighter checkpoints;
/// see <see cref="LamaModelFiles"/> for their sources and overrides.
/// </summary>
public enum LamaModelVariant
{
    Small,
    Middle,
    Large
}

/// <summary>A selectable LaMa checkpoint shown in the Uncrop UI.</summary>
public sealed record LamaModelOption(LamaModelVariant Variant, string DisplayName, int ApproxSizeMb);

/// <summary>
/// LaMa (large-mask inpainting) ONNX model registry. Each variant maps to a file name + download
/// URL; every URL can be overridden with an environment variable (BIR_LAMA_SMALL_URL /
/// BIR_LAMA_MIDDLE_URL / BIR_LAMA_URL) or the file can be placed manually in the models cache
/// folder as a fallback (see <see cref="ModelCacheService"/>).
/// </summary>
public static class LamaModelFiles
{
    public static readonly IReadOnlyList<LamaModelOption> All = new[]
    {
        new LamaModelOption(LamaModelVariant.Small, "Small LaMa (fast)", 45),
        new LamaModelOption(LamaModelVariant.Middle, "Middle LaMa", 200),
        new LamaModelOption(LamaModelVariant.Large, "Big LaMa (default)", 208)
    };

    /// <summary>Display option for a variant, or the default (Large) for unknown values.</summary>
    public static LamaModelOption Option(LamaModelVariant variant)
        => All.FirstOrDefault(o => o.Variant == variant) ?? All[^1];

    /// <summary>Cache file name for a variant.</summary>
    public static string FileName(LamaModelVariant variant) => variant switch
    {
        LamaModelVariant.Small => "lama_small.onnx",
        LamaModelVariant.Middle => "lama_middle.onnx",
        _ => "lama_fp32.onnx"
    };

    /// <summary>Download URL for a variant, honoring the BIR_LAMA_* environment override.</summary>
    public static string Url(LamaModelVariant variant) => variant switch
    {
        LamaModelVariant.Small => Env("BIR_LAMA_SMALL_URL",
            "https://huggingface.co/DeningX/lama/resolve/main/lama_small.onnx"),
        LamaModelVariant.Middle => Env("BIR_LAMA_MIDDLE_URL",
            "https://huggingface.co/DeningX/lama/resolve/main/lama_middle.onnx"),
        _ => Env("BIR_LAMA_URL",
            "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx")
    };

    private static string Env(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } u ? u : fallback;
}
