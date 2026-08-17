using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Preview;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Sam;

/// <summary>
/// Runs MobileSAM's two-stage click segmentation: an expensive image encoder run once per
/// image (cached as a <see cref="SamEmbedding"/>), and a cheap decoder run per click that
/// accepts a foreground point plus the desired output size (the exported decoder graph does
/// the final upsample to that size internally, so no manual resize/crop math is needed here).
/// </summary>
public sealed class SamInferenceEngine : IDisposable
{
    private const int EncoderInputSize = 1024;
    private const int LowResMaskSize = 256;

    // SAM's image encoder was trained with ImageNet-style pixel-value (0-255) normalization,
    // not the 0-1 range used by U2Net.
    private static readonly float[] Mean = { 123.675f, 116.28f, 103.53f };
    private static readonly float[] Std = { 58.395f, 57.12f, 57.375f };

    private readonly IModelCacheService _modelCache;
    private InferenceSession? _encoder;
    private InferenceSession? _decoder;

    public SamInferenceEngine(IModelCacheService modelCache)
    {
        _modelCache = modelCache;
    }

    public bool IsReady => _encoder is not null && _decoder is not null;

    public async Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (IsReady)
        {
            return;
        }

        string encoderPath = await _modelCache.EnsureNamedFileAvailableAsync(
            SamModelFiles.EncoderFileName, SamModelFiles.EncoderUrl, progress, ct);
        string decoderPath = await _modelCache.EnsureNamedFileAvailableAsync(
            SamModelFiles.DecoderFileName, SamModelFiles.DecoderUrl, progress, ct);

        _encoder = new InferenceSession(encoderPath);
        _decoder = new InferenceSession(decoderPath);
    }

    public SamEmbedding ComputeEmbedding(Mat bgr)
    {
        if (_encoder is null)
        {
            throw new InvalidOperationException("Call EnsureReadyAsync before ComputeEmbedding.");
        }

        var size = ImageScaling.ComputeFitSize(bgr.Width, bgr.Height, EncoderInputSize);

        using var resized = new Mat();
        Cv2.Resize(bgr, resized, size, interpolation: InterpolationFlags.Area);
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        using var padded = new Mat(EncoderInputSize, EncoderInputSize, MatType.CV_8UC3, Scalar.All(0));
        using (var roi = new Mat(padded, new Rect(0, 0, size.Width, size.Height)))
        {
            rgb.CopyTo(roi);
        }

        padded.GetArray(out Vec3b[] pixels);
        var input = new DenseTensor<float>(new[] { 1, 3, EncoderInputSize, EncoderInputSize });
        for (int y = 0; y < EncoderInputSize; y++)
        {
            for (int x = 0; x < EncoderInputSize; x++)
            {
                var px = pixels[y * EncoderInputSize + x];
                input[0, 0, y, x] = (px.Item0 - Mean[0]) / Std[0];
                input[0, 1, y, x] = (px.Item1 - Mean[1]) / Std[1];
                input[0, 2, y, x] = (px.Item2 - Mean[2]) / Std[2];
            }
        }

        var inputName = _encoder.InputMetadata.Keys.First();
        using var results = _encoder.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        // Copy out of the OrtValue-backed tensor before `results` is disposed.
        var embeddingData = new DenseTensor<float>(output.ToArray(), output.Dimensions.ToArray());

        return new SamEmbedding { Data = embeddingData, SourceImageSize = bgr.Size() };
    }

    /// <summary>Runs the decoder for one foreground point, returning a 0-255 mask sized to <paramref name="targetSize"/>.</summary>
    public Mat InferMask(SamEmbedding embedding, Size targetSize, Point promptPoint)
    {
        if (_decoder is null)
        {
            throw new InvalidOperationException("Call EnsureReadyAsync before InferMask.");
        }

        var pointCoords = new DenseTensor<float>(new[] { 1, 1, 2 });
        pointCoords[0, 0, 0] = promptPoint.X;
        pointCoords[0, 0, 1] = promptPoint.Y;
        var pointLabels = new DenseTensor<float>(new[] { 1, 1 });
        pointLabels[0, 0] = 1f; // 1 = foreground click

        var maskInput = new DenseTensor<float>(new[] { 1, 1, LowResMaskSize, LowResMaskSize });
        var hasMaskInput = new DenseTensor<float>(new[] { 1 });
        var origImSize = new DenseTensor<float>(new[] { 2 });
        origImSize[0] = targetSize.Height;
        origImSize[1] = targetSize.Width;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("image_embeddings", embedding.Data),
            NamedOnnxValue.CreateFromTensor("point_coords", pointCoords),
            NamedOnnxValue.CreateFromTensor("point_labels", pointLabels),
            NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
            NamedOnnxValue.CreateFromTensor("has_mask_input", hasMaskInput),
            NamedOnnxValue.CreateFromTensor("orig_im_size", origImSize),
        };

        using var results = _decoder.Run(inputs);
        var masksTensor = results.First(r => r.Name.Contains("mask", StringComparison.OrdinalIgnoreCase)).AsTensor<float>();

        int h = targetSize.Height, w = targetSize.Width;
        var maskBytes = new byte[h * w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                maskBytes[y * w + x] = masksTensor[0, 0, y, x] > 0 ? (byte)255 : (byte)0;
            }
        }

        using var mask = new Mat(h, w, MatType.CV_8UC1);
        mask.SetArray(maskBytes);

        var feathered = new Mat();
        Cv2.GaussianBlur(mask, feathered, new Size(5, 5), 0);
        return feathered;
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _decoder?.Dispose();
    }
}
