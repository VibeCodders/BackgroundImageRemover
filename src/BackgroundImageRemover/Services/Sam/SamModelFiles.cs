namespace BackgroundImageRemover.Services.Sam;

/// <summary>
/// MobileSAM ONNX encoder/decoder file names and download URLs. Both are public release
/// assets; either can be overridden via BIR_SAM_ENCODER_URL / BIR_SAM_DECODER_URL if the
/// asset ever moves, or downloaded manually into the models cache folder as a fallback.
/// </summary>
public static class SamModelFiles
{
    public const string EncoderFileName = "mobile_sam.encoder.onnx";
    public const string DecoderFileName = "mobile_sam.decoder.onnx";

    private const string DefaultEncoderUrl = "https://github.com/vietanhdev/samexporter/releases/download/v1.0/mobile_sam.encoder.onnx";
    private const string DefaultDecoderUrl = "https://github.com/vietanhdev/samexporter/releases/download/v1.0/mobile_sam.decoder.onnx";

    public static string EncoderUrl => Environment.GetEnvironmentVariable("BIR_SAM_ENCODER_URL") is { Length: > 0 } u ? u : DefaultEncoderUrl;
    public static string DecoderUrl => Environment.GetEnvironmentVariable("BIR_SAM_DECODER_URL") is { Length: > 0 } u ? u : DefaultDecoderUrl;
}
