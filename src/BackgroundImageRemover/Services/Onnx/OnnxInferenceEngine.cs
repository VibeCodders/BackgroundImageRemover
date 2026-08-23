using System.Buffers;
using System.Threading.Tasks;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SimdLinq;

namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Owns cached <see cref="InferenceSession"/>s (one per model, loaded lazily) and runs
/// saliency inference at each model's input size, returning a single-channel mask resized to
/// match the requested output size.
/// </summary>
public sealed class OnnxInferenceEngine : IDisposable
{
    private readonly IModelCacheService _modelCache;
    private readonly IFileLogService _log;
    private readonly Dictionary<OnnxModelKind, InferenceSession> _sessions = new();

    /// <summary>
    /// Whether to try DirectML (GPU) when the next session is created. Changing this only
    /// affects models loaded afterwards; already-loaded sessions keep their execution provider
    /// until <see cref="ReleaseAllSessions"/> is called.
    /// </summary>
    public bool UseGpu { get; set; }

    public OnnxInferenceEngine(IModelCacheService modelCache, IFileLogService log)
    {
        _modelCache = modelCache;
        _log = log;
    }

    public bool IsReady(OnnxModelKind kind) => _sessions.ContainsKey(kind);

    public async Task EnsureReadyAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (_sessions.ContainsKey(kind))
        {
            return;
        }

        string path = await _modelCache.EnsureModelAvailableAsync(kind, progress, ct);
        _sessions[kind] = CreateSession(path);
    }

    private InferenceSession CreateSession(string path)
    {
        if (UseGpu)
        {
            try
            {
                var options = new SessionOptions();
                options.AppendExecutionProvider_DML(deviceId: 0);
                return new InferenceSession(path, options);
            }
            catch (Exception ex)
            {
                _log.Error("DirectML execution provider unavailable, falling back to CPU.", ex);
            }
        }
        return new InferenceSession(path);
    }

    /// <summary>Disposes all cached sessions so the next <see cref="EnsureReadyAsync"/> call rebuilds them (e.g. after toggling GPU use).</summary>
    public void ReleaseAllSessions()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }

    /// <summary>Runs saliency inference and returns a single-channel 0-255 mask sized to <paramref name="bgr"/>.</summary>
    public Mat InferMask(Mat bgr, OnnxModelKind kind)
    {
        if (!_sessions.TryGetValue(kind, out var session))
        {
            throw new InvalidOperationException("Call EnsureReadyAsync before InferMask.");
        }

        var definition = OnnxModelCatalog.Get(kind);
        int inputSize = definition.InputSize;

        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(inputSize, inputSize), interpolation: InterpolationFlags.Area);

        // Precompute scale/offset per channel so each pixel is a multiply-add, not two
        // divisions: (px / 255 - mean) / std == px * invScale - offset.
        int plane = inputSize * inputSize;
        float[] scale = new float[3];
        float[] offset = new float[3];
        for (int c = 0; c < 3; c++)
        {
            scale[c] = 1f / (255f * definition.Std[c]);
            offset[c] = definition.Mean[c] / definition.Std[c];
        }

        // The input tensor's backing buffer is rented from the shared pool (3*plane floats,
        // ~3 MB at 1024²) and returned after the run, instead of allocating a fresh array per
        // segmentation. Rows are independent, so the fill runs in parallel and reads the
        // resized BGR Mat directly through its native buffer — the BGR→RGB swap is folded into
        // the channel writes (R = Item2, G = Item1, B = Item0), eliminating the full-image
        // CvtColor pass that used to precede the fill.
        int tensorLength = 3 * plane;
        float[] rented = ArrayPool<float>.Shared.Rent(tensorLength);
        using var smallMask = new Mat(inputSize, inputSize, MatType.CV_8UC1);
        try
        {
            var input = new DenseTensor<float>(rented.AsMemory(0, tensorLength), new[] { 1, 3, inputSize, inputSize });
            var inputMem = input.Buffer;
            unsafe
            {
                byte* srcPtr = (byte*)resized.DataPointer;
                long srcStep = resized.Step();
                Parallel.For(0, inputSize, y =>
                {
                    var row = new Span<Vec3b>((Vec3b*)(srcPtr + y * srcStep), inputSize);
                    var inputSpan = inputMem.Span;
                    int i = y * inputSize;
                    for (int x = 0; x < inputSize; x++)
                    {
                        var px = row[x];
                        inputSpan[i] = px.Item2 * scale[0] - offset[0]; // R
                        inputSpan[plane + i] = px.Item1 * scale[1] - offset[1]; // G
                        inputSpan[2 * plane + i] = px.Item0 * scale[2] - offset[2]; // B
                        i++;
                    }
                });
            }

            var inputName = session.InputMetadata.Keys.First();
            using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
            var output = results.First().AsTensor<float>();

            // DenseTensor<float> exposes its backing buffer as a span: rescale the flat data
            // in place with a SIMD pass (ZLinqPixelOps) after computing the range with
            // SIMD-accelerated min/max from SimdLinq, then write the bytes straight into the
            // mask Mat's buffer (no intermediate array), in parallel.
            if (output is DenseTensor<float> dense)
            {
                var outputMem = dense.Buffer;
                (float min, float max) = outputMem.Span.MinMax();
                float range = Math.Max(1e-6f, max - min);
                ZLinqPixelOps.NormalizeMaskToByteRange(outputMem.Span, min, range);
                unsafe
                {
                    byte* maskPtr = (byte*)smallMask.DataPointer;
                    long maskStep = smallMask.Step();
                    Parallel.For(0, inputSize, y =>
                    {
                        var maskRow = new Span<byte>((byte*)(maskPtr + y * maskStep), inputSize);
                        var outputSpan = outputMem.Span;
                        int i = y * inputSize;
                        for (int x = 0; x < inputSize; x++)
                        {
                            maskRow[x] = (byte)Math.Clamp(outputSpan[i], 0f, 255f);
                            i++;
                        }
                    });
                }
            }
            else
            {
                float[] fallback = output.ToArray();
                (float min, float max) = fallback.MinMax();
                float range = Math.Max(1e-6f, max - min);
                ZLinqPixelOps.NormalizeMaskToByteRange(fallback, min, range);
                var maskBytes = new byte[inputSize * inputSize];
                for (int i = 0; i < maskBytes.Length; i++)
                {
                    maskBytes[i] = (byte)Math.Clamp(fallback[i], 0f, 255f);
                }
                smallMask.SetArray(maskBytes);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(rented);
        }

        var fullMask = new Mat();
        Cv2.Resize(smallMask, fullMask, bgr.Size(), interpolation: InterpolationFlags.Linear);
        return fullMask;
    }

    public void Dispose() => ReleaseAllSessions();
}
