using BackgroundImageRemover.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Owns cached <see cref="InferenceSession"/>s (one per model, loaded lazily) and runs
/// saliency inference at each model's input size, returning a single-channel mask resized to
/// match the requested output size.
/// </summary>
public sealed class OnnxInferenceEngine : IDisposable
{
    private readonly IModelCacheService _modelCache;
    private readonly Dictionary<OnnxModelKind, InferenceSession> _sessions = new();

    public OnnxInferenceEngine(IModelCacheService modelCache)
    {
        _modelCache = modelCache;
    }

    public bool IsReady(OnnxModelKind kind) => _sessions.ContainsKey(kind);

    public async Task EnsureReadyAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (_sessions.ContainsKey(kind))
        {
            return;
        }

        string path = await _modelCache.EnsureModelAvailableAsync(kind, progress, ct);
        _sessions[kind] = new InferenceSession(path);
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
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                var px = pixels[y * inputSize + x];
                input[0, 0, y, x] = (px.Item0 / 255f - definition.Mean[0]) / definition.Std[0];
                input[0, 1, y, x] = (px.Item1 / 255f - definition.Mean[1]) / definition.Std[1];
                input[0, 2, y, x] = (px.Item2 / 255f - definition.Mean[2]) / definition.Std[2];
            }
        }

        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        var maskBytes = new byte[inputSize * inputSize];
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < maskBytes.Length; i++)
        {
            float v = output.GetValue(i);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float range = Math.Max(1e-6f, max - min);
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                float v = (output[0, 0, y, x] - min) / range;
                maskBytes[y * inputSize + x] = (byte)Math.Clamp(v * 255f, 0f, 255f);
            }
        }

        using var smallMask = new Mat(inputSize, inputSize, MatType.CV_8UC1);
        smallMask.SetArray(maskBytes);

        var fullMask = new Mat();
        Cv2.Resize(smallMask, fullMask, bgr.Size(), interpolation: InterpolationFlags.Linear);
        return fullMask;
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }
}
