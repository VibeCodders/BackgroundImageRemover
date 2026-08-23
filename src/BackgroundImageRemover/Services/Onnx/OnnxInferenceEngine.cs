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
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        rgb.GetArray(out Vec3b[] pixels);
        var input = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
        // The tensor is contiguous [1,3,H,W]: fill its buffer directly (CHW layout), one
        // channel plane at a time, instead of going through the 4-D indexer per pixel.
        // Precompute scale/offset per channel so each pixel is a multiply-add, not two
        // divisions: (px / 255 - mean) / std == px * invScale - offset.
        var inputSpan = input.Buffer.Span;
        int plane = inputSize * inputSize;
        float[] scale = new float[3];
        float[] offset = new float[3];
        for (int c = 0; c < 3; c++)
        {
            scale[c] = 1f / (255f * definition.Std[c]);
            offset[c] = definition.Mean[c] / definition.Std[c];
        }
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                var px = pixels[y * inputSize + x];
                int i = y * inputSize + x;
                inputSpan[i] = px.Item0 * scale[0] - offset[0];
                inputSpan[plane + i] = px.Item1 * scale[1] - offset[1];
                inputSpan[2 * plane + i] = px.Item2 * scale[2] - offset[2];
            }
        }

        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        // DenseTensor<float> exposes its backing buffer as a span: normalize and rescale over
        // the flat data, using SIMD-accelerated min/max from SimdLinq for the range.
        var maskBytes = new byte[inputSize * inputSize];
        if (output is DenseTensor<float> dense)
        {
            var outputSpan = dense.Buffer.Span;
            (float min, float max) = outputSpan.MinMax();
            float range = Math.Max(1e-6f, max - min);
            for (int i = 0; i < maskBytes.Length; i++)
            {
                float v = (outputSpan[i] - min) / range;
                maskBytes[i] = (byte)Math.Clamp(v * 255f, 0f, 255f);
            }
        }
        else
        {
            float[] fallback = output.ToArray();
            (float min, float max) = fallback.MinMax();
            float range = Math.Max(1e-6f, max - min);
            for (int i = 0; i < maskBytes.Length; i++)
            {
                float v = (fallback[i] - min) / range;
                maskBytes[i] = (byte)Math.Clamp(v * 255f, 0f, 255f);
            }
        }

        using var smallMask = new Mat(inputSize, inputSize, MatType.CV_8UC1);
        smallMask.SetArray(maskBytes);

        var fullMask = new Mat();
        Cv2.Resize(smallMask, fullMask, bgr.Size(), interpolation: InterpolationFlags.Linear);
        return fullMask;
    }

    public void Dispose() => ReleaseAllSessions();
}
