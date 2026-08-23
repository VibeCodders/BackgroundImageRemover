using BackgroundImageRemover.Helpers;
using BenchmarkDotNet.Attributes;
using OpenCvSharp;

namespace BackgroundImageRemover.Benchmarks;

/// <summary>
/// <c>BlendByMask</c> is the shared mask blend used by blur, sharpen, dodge/burn, gradient,
/// vignette, shape, tilt-shift and mosaic on every preview/apply. It went from ~7 intermediate
/// CV_32FC3 Mats (~9 full-image passes) to a single zero-copy parallel pass over the native
/// buffers (Span2D + parallel rows). The byte overlay mirrors a blur/sharpen preview; the float
/// overlay mirrors VignetteService's <c>aF * 0.6</c> overlay.
/// </summary>
[MemoryDiagnoser]
public class BlendByMaskBenchmarks
{
    private Mat _base = null!;
    private Mat _byteOverlay = null!;
    private Mat _floatOverlay = null!;
    private Mat _mask = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _base = new Mat(1920, 1080, MatType.CV_8UC3);
        _byteOverlay = new Mat(1920, 1080, MatType.CV_8UC3);
        _floatOverlay = new Mat(1920, 1080, MatType.CV_32FC3);
        _mask = new Mat(1920, 1080, MatType.CV_8UC1);
        var baseSpan = _base.AsSpan2D<Vec3b>();
        var byteSpan = _byteOverlay.AsSpan2D<Vec3b>();
        var floatSpan = _floatOverlay.AsSpan2D<Vec3f>();
        var maskSpan = _mask.AsSpan2D<byte>();
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                baseSpan[y, x] = new Vec3b((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
                byteSpan[y, x] = new Vec3b((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
                floatSpan[y, x] = new Vec3f((float)rng.NextDouble() * 255f, (float)rng.NextDouble() * 255f, (float)rng.NextDouble() * 255f);
                maskSpan[y, x] = (byte)rng.Next(256);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _base.Dispose();
        _byteOverlay.Dispose();
        _floatOverlay.Dispose();
        _mask.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Mat ByteOverlay() => _base.BlendByMask(_byteOverlay, _mask);

    [Benchmark]
    public Mat FloatOverlay() => _base.BlendByMask(_floatOverlay, _mask);
}
