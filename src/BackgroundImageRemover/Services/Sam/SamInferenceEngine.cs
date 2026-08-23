using System.Threading.Tasks;
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

        var input = new DenseTensor<float>(new[] { 1, 3, EncoderInputSize, EncoderInputSize });
        // Contiguous [1,3,H,W] tensor: fill the flat buffer directly (CHW layout).
        // Precompute scale/offset per channel: (px - mean) / std == px * scale - offset.
        // Rows are independent, so the fill runs in parallel; the padded Mat is read directly
        // through its native buffer, avoiding the per-run GetArray copy (1024x1024 = 1M pixels).
        var inputMem = input.Buffer;
        int plane = EncoderInputSize * EncoderInputSize;
        float[] scale = new float[3];
        float[] offset = new float[3];
        for (int c = 0; c < 3; c++)
        {
            scale[c] = 1f / Std[c];
            offset[c] = Mean[c] / Std[c];
        }
        unsafe
        {
            byte* srcPtr = (byte*)padded.DataPointer;
            long srcStep = padded.Step();
            Parallel.For(0, EncoderInputSize, y =>
            {
                var row = new Span<Vec3b>((Vec3b*)(srcPtr + y * srcStep), EncoderInputSize);
                var inputSpan = inputMem.Span;
                int i = y * EncoderInputSize;
                for (int x = 0; x < EncoderInputSize; x++)
                {
                    var px = row[x];
                    inputSpan[i] = px.Item0 * scale[0] - offset[0];
                    inputSpan[plane + i] = px.Item1 * scale[1] - offset[1];
                    inputSpan[2 * plane + i] = px.Item2 * scale[2] - offset[2];
                    i++;
                }
            });
        }

        var inputName = _encoder.InputMetadata.Keys.First();
        using var results = _encoder.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        // Copy out of the OrtValue-backed tensor before `results` is disposed.
        var embeddingData = new DenseTensor<float>(output.ToArray(), output.Dimensions.ToArray());

        return new SamEmbedding { Data = embeddingData, SourceImageSize = bgr.Size() };
    }

    /// <summary>Runs the decoder for one or more foreground points, returning a 0-255 mask sized to <paramref name="targetSize"/>.</summary>
    public Mat InferMask(SamEmbedding embedding, Size targetSize, params Point[] promptPoints)
    {
        if (_decoder is null)
        {
            throw new InvalidOperationException("Call EnsureReadyAsync before InferMask.");
        }

        if (promptPoints is null || promptPoints.Length == 0)
        {
            throw new ArgumentException("At least one prompt point is required.", nameof(promptPoints));
        }

        int n = promptPoints.Length;
        var pointCoords = new DenseTensor<float>(new[] { 1, n, 2 });
        for (int i = 0; i < n; i++)
        {
            pointCoords[0, i, 0] = promptPoints[i].X;
            pointCoords[0, i, 1] = promptPoints[i].Y;
        }

        var pointLabels = new DenseTensor<float>(new[] { 1, n });
        for (int i = 0; i < n; i++)
        {
            pointLabels[0, i] = 1f; // 1 = foreground click
        }

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
        using var mask = new Mat(h, w, MatType.CV_8UC1);
        if (masksTensor is DenseTensor<float> dense)
        {
            // Threshold the flat buffer straight into the mask Mat (no intermediate array),
            // in parallel over rows.
            var outputMem = dense.Buffer;
            unsafe
            {
                byte* maskPtr = (byte*)mask.DataPointer;
                long maskStep = mask.Step();
                Parallel.For(0, h, y =>
                {
                    var maskRow = new Span<byte>((byte*)(maskPtr + y * maskStep), w);
                    var span = outputMem.Span;
                    int i = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        maskRow[x] = span[i] > 0 ? (byte)255 : (byte)0;
                        i++;
                    }
                });
            }
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    mask.Set(y, x, masksTensor[0, 0, y, x] > 0 ? (byte)255 : (byte)0);
                }
            }
        }

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
