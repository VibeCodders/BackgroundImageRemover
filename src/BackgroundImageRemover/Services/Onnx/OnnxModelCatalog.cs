using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Services.Onnx;

public sealed record OnnxModelDefinition(
    OnnxModelKind Kind,
    string DisplayName,
    string FileName,
    string DownloadUrl,
    int InputSize,
    float[] Mean,
    float[] Std);

/// <summary>
/// Known ONNX saliency/segmentation models and where to download each from. All are hosted as
/// public release assets; URLs can be overridden per-model via the BIR_<KIND>_URL environment
/// variable (e.g. BIR_U2NET_URL) if an asset ever moves.
/// </summary>
public static class OnnxModelCatalog
{
    private static readonly float[] ImageNetMean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] ImageNetStd = { 0.229f, 0.224f, 0.225f };
    private static readonly float[] UnitMean = { 0f, 0f, 0f };
    private static readonly float[] UnitStd = { 1f, 1f, 1f };

    private static readonly IReadOnlyDictionary<OnnxModelKind, OnnxModelDefinition> Definitions =
        new Dictionary<OnnxModelKind, OnnxModelDefinition>
        {
            [OnnxModelKind.U2NetP] = new(
                OnnxModelKind.U2NetP, "U2Net-p (fast, ~4MB)", "u2netp.onnx",
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx",
                320, ImageNetMean, ImageNetStd),

            [OnnxModelKind.U2Net] = new(
                OnnxModelKind.U2Net, "U2Net (high quality, ~176MB)", "u2net.onnx",
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx",
                320, ImageNetMean, ImageNetStd),

            [OnnxModelKind.Silueta] = new(
                OnnxModelKind.Silueta, "Silueta (small, general purpose)", "silueta.onnx",
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/silueta.onnx",
                320, ImageNetMean, ImageNetStd),

            [OnnxModelKind.Rmbg14] = new(
                OnnxModelKind.Rmbg14, "RMBG-1.4 (BRIA, high quality)", "rmbg14.onnx",
                "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx",
                1024, UnitMean, UnitStd),

            [OnnxModelKind.BriaRm] = new(
                OnnxModelKind.BriaRm, "BiRefNet (large, best quality)", "bria_rm.onnx",
                "https://github.com/danielgatis/rembg/releases/download/v0.0.0/bria_rm.onnx",
                1024, UnitMean, UnitStd),
        };

    public static OnnxModelDefinition Get(OnnxModelKind kind)
    {
        var def = Definitions[kind];
        string envKey = $"BIR_{kind.ToString().ToUpperInvariant()}_URL";
        string? overrideUrl = Environment.GetEnvironmentVariable(envKey);
        return overrideUrl is { Length: > 0 } ? def with { DownloadUrl = overrideUrl } : def;
    }

    public static IEnumerable<OnnxModelDefinition> All => Definitions.Values;
}
