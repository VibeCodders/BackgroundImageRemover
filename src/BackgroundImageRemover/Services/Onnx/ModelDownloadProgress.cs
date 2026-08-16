namespace BackgroundImageRemover.Services.Onnx;

public readonly record struct ModelDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? FractionComplete => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;
}
