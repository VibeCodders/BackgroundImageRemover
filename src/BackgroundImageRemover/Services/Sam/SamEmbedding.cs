using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Sam;

/// <summary>Cached output of the SAM image encoder for one image, reused across clicks/output sizes.</summary>
public sealed class SamEmbedding
{
    public required DenseTensor<float> Data { get; init; }
    public required Size SourceImageSize { get; init; }
}
