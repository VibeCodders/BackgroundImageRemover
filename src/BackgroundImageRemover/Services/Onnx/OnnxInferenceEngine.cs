using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Owns a cached U2Netp <see cref="InferenceSession"/> and runs saliency inference at the
/// model's fixed 320x320 input size, returning a single-channel mask resized to match the
/// requested output size.
/// </summary>
public sealed class OnnxInferenceEngine : IDisposable
{
    private const int InputSize = 320;
    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    private readonly IModelCacheService _modelCache;
    private InferenceSession? _session;

    public OnnxInferenceEngine(IModelCacheService modelCache)
    {
        _modelCache = modelCache;
    }

    public bool IsReady => _session is not null;

    public async Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        if (_session is not null)
        {
            return;
        }

        string path = await _modelCache.EnsureModelAvailableAsync(progress, ct);
        _session = new InferenceSession(path);
    }

    /// <summary>Runs saliency inference and returns a single-channel 0-255 mask sized to <paramref name="bgr"/>.</summary>
    public Mat InferMask(Mat bgr)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("Call EnsureReadyAsync before InferMask.");
        }

        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(InputSize, InputSize), interpolation: InterpolationFlags.Area);
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        rgb.GetArray(out Vec3b[] pixels);
        var input = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        for (int y = 0; y < InputSize; y++)
        {
            for (int x = 0; x < InputSize; x++)
            {
                var px = pixels[y * InputSize + x];
                input[0, 0, y, x] = (px.Item0 / 255f - Mean[0]) / Std[0];
                input[0, 1, y, x] = (px.Item1 / 255f - Mean[1]) / Std[1];
                input[0, 2, y, x] = (px.Item2 / 255f - Mean[2]) / Std[2];
            }
        }

        var inputName = _session.InputMetadata.Keys.First();
        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, input) });
        var output = results.First().AsTensor<float>();

        var maskBytes = new byte[InputSize * InputSize];
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < maskBytes.Length; i++)
        {
            float v = output.GetValue(i);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float range = Math.Max(1e-6f, max - min);
        for (int y = 0; y < InputSize; y++)
        {
            for (int x = 0; x < InputSize; x++)
            {
                float v = (output[0, 0, y, x] - min) / range;
                maskBytes[y * InputSize + x] = (byte)Math.Clamp(v * 255f, 0f, 255f);
            }
        }

        using var smallMask = new Mat(InputSize, InputSize, MatType.CV_8UC1);
        smallMask.SetArray(maskBytes);

        var fullMask = new Mat();
        Cv2.Resize(smallMask, fullMask, bgr.Size(), interpolation: InterpolationFlags.Linear);
        return fullMask;
    }

    public void Dispose() => _session?.Dispose();
}
