using BackgroundImageRemover.Services.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Onnx;

/// <summary>
/// Owns the lazily-loaded LaMa <see cref="InferenceSession"/> and runs image inpainting. The
/// Carve/LaMa-ONNX export takes a fixed 512×512 input: <c>image</c> (RGB float32 0-1) and
/// <c>mask</c> (float32 0/1), and returns <c>output</c> (float32 0-255) where the unmasked
/// region already contains the original pixels (the export composites <c>mask*pred +
/// (1-mask)*image</c> and clamps to 0-255 inside the graph). Input names are detected from the
/// session metadata by channel count so a differently-named export still works.
/// </summary>
public sealed class LamaInpaintEngine : ILamaInpaintEngine
{
    public const int ModelSize = 512;

    private readonly IModelCacheService _modelCache;
    private readonly IFileLogService _log;
    private readonly object _gate = new();
    private InferenceSession? _session;
    private LamaModelVariant _loadedVariant;
    private bool _loadedGpu;

    public LamaInpaintEngine(IModelCacheService modelCache, IFileLogService log)
    {
        _modelCache = modelCache;
        _log = log;
    }

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _session is not null;
            }
        }
    }

    public async Task EnsureReadyAsync(LamaModelVariant variant, bool useGpu, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_session is not null && _loadedVariant == variant && _loadedGpu == useGpu)
            {
                return;
            }
        }

        string path = await _modelCache.EnsureNamedFileAvailableAsync(
            LamaModelFiles.FileName(variant), LamaModelFiles.Url(variant), progress, ct);

        var session = CreateSession(path, useGpu);
        lock (_gate)
        {
            // Another caller may have finished loading the same combo while we were downloading.
            if (_session is not null && _loadedVariant == variant && _loadedGpu == useGpu)
            {
                session.Dispose();
                return;
            }

            var previous = _session;
            _session = session;
            _loadedVariant = variant;
            _loadedGpu = useGpu;
            // Switching model/device drops the previous session so memory stays bounded; the
            // UI serializes uncrop operations, so no in-flight run is holding it.
            previous?.Dispose();
        }
    }

    /// <summary>
    /// Creates the session, requesting the DirectML EP when <paramref name="useGpu"/> is set.
    /// In builds without DirectML support the option is ignored; when the EP is requested but
    /// unavailable (no DX12 GPU/driver), the session falls back to the CPU EP and the fallback
    /// is logged so the user can see why GPU acceleration is not active.
    /// </summary>
    private InferenceSession CreateSession(string path, bool useGpu)
    {
        if (!useGpu)
        {
            return new InferenceSession(path);
        }

#if DIRECTML_ENABLED
        var options = new SessionOptions();
        try
        {
            options.AppendExecutionProvider_DML();
        }
        catch (Exception ex)
        {
            _log.Info($"DirectML provider unavailable, running LaMa on CPU: {ex.Message}");
            return new InferenceSession(path);
        }

        try
        {
            return new InferenceSession(path, options);
        }
        catch (Exception ex)
        {
            _log.Info($"DirectML session failed, falling back to CPU: {ex.Message}");
            return new InferenceSession(path);
        }
#else
        // Build without the DirectML package: honor the request but stay on the CPU EP.
        _log.Info("DirectML is not included in this build; running LaMa on CPU.");
        return new InferenceSession(path);
#endif
    }

    public Mat Inpaint(Mat imageBgr512, Mat mask512)
    {
        ArgumentNullException.ThrowIfNull(imageBgr512);
        ArgumentNullException.ThrowIfNull(mask512);
        if (imageBgr512.Width != ModelSize || imageBgr512.Height != ModelSize)
        {
            throw new ArgumentException($"LaMa requires {ModelSize}x{ModelSize} input images.", nameof(imageBgr512));
        }

        InferenceSession session;
        lock (_gate)
        {
            session = _session ?? throw new InvalidOperationException("Call EnsureReadyAsync before Inpaint.");
        }

        var (imageName, maskName) = FindInputs(session);
        var imageTensor = new DenseTensor<float>(new[] { 1, 3, ModelSize, ModelSize });
        var maskTensor = new DenseTensor<float>(new[] { 1, 1, ModelSize, ModelSize });
        FillImageTensor(imageBgr512, imageTensor);
        FillMaskTensor(mask512, maskTensor);

        using var results = session.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor(imageName, imageTensor),
            NamedOnnxValue.CreateFromTensor(maskName, maskTensor)
        });

        var output = results.First().AsTensor<float>();
        if (output is DenseTensor<float> dense)
        {
            return TensorToBgr(dense);
        }

        // Non-dense tensor (unexpected for this model): materialize a copy so TensorToBgr can
        // read a contiguous buffer.
        var copy = new DenseTensor<float>(output.ToArray(), output.Dimensions.ToArray());
        return TensorToBgr(copy);
    }

    /// <summary>Locates the image and mask inputs: known names first, then declaration order.</summary>
    internal static (string Image, string Mask) FindInputs(InferenceSession session)
    {
        var keys = session.InputMetadata.Keys.ToArray();
        string imageName = keys.FirstOrDefault(k => k.Equals("image", StringComparison.OrdinalIgnoreCase))
            ?? keys.FirstOrDefault();
        string maskName = keys.FirstOrDefault(k => k.Equals("mask", StringComparison.OrdinalIgnoreCase))
            ?? keys.FirstOrDefault(k => k != imageName);
        if (imageName is null || maskName is null)
        {
            throw new InvalidOperationException("LaMa model must expose image and mask inputs.");
        }
        return (imageName, maskName);
    }

    /// <summary>Writes a square BGR Mat into the CHW image tensor as RGB scaled to 0-1, in parallel rows.</summary>
    internal static void FillImageTensor(Mat imageBgr, DenseTensor<float> imageTensor)
    {
        int size = imageTensor.Dimensions[2];
        int plane = size * size;
        var mem = imageTensor.Buffer;
        unsafe
        {
            byte* srcPtr = (byte*)imageBgr.DataPointer;
            long srcStep = imageBgr.Step();
            Parallel.For(0, size, y =>
            {
                var row = new Span<Vec3b>((Vec3b*)(srcPtr + y * srcStep), size);
                var span = mem.Span;
                int i = y * size;
                for (int x = 0; x < size; x++)
                {
                    var px = row[x];
                    span[i] = px.Item2 / 255f;        // R
                    span[plane + i] = px.Item1 / 255f; // G
                    span[2 * plane + i] = px.Item0 / 255f; // B
                    i++;
                }
            });
        }
    }

    /// <summary>Writes a square 0/255 byte mask into the CHW mask tensor as 0/1 floats, in parallel rows.</summary>
    internal static void FillMaskTensor(Mat mask, DenseTensor<float> maskTensor)
    {
        int size = maskTensor.Dimensions[2];
        int plane = size * size;
        var mem = maskTensor.Buffer;
        unsafe
        {
            byte* srcPtr = (byte*)mask.DataPointer;
            long srcStep = mask.Step();
            Parallel.For(0, size, y =>
            {
                var row = new Span<byte>((byte*)(srcPtr + y * srcStep), size);
                var span = mem.Span;
                int i = y * size;
                for (int x = 0; x < size; x++)
                {
                    span[i] = row[x] > 0 ? 1f : 0f;
                    i++;
                }
            });
        }
    }

    /// <summary>Reads a [1,3,S,S] float tensor (0-255) into a square BGR Mat, in parallel rows.</summary>
    internal static Mat TensorToBgr(DenseTensor<float> output)
    {
        int size = output.Dimensions[2];
        int plane = size * size;
        var bgr = new Mat(size, size, MatType.CV_8UC3);
        var mem = output.Buffer;
        unsafe
        {
            byte* dstPtr = (byte*)bgr.DataPointer;
            long dstStep = bgr.Step();
            Parallel.For(0, size, y =>
            {
                var row = new Span<Vec3b>((Vec3b*)(dstPtr + y * dstStep), size);
                var span = mem.Span;
                int i = y * size;
                for (int x = 0; x < size; x++)
                {
                    byte r = (byte)Math.Clamp(span[i], 0f, 255f);
                    byte g = (byte)Math.Clamp(span[plane + i], 0f, 255f);
                    byte b = (byte)Math.Clamp(span[2 * plane + i], 0f, 255f);
                    row[x] = new Vec3b(b, g, r);
                    i++;
                }
            });
        }
        return bgr;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
