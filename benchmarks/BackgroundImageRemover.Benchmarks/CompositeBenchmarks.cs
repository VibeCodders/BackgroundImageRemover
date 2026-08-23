using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Compositing;
using BenchmarkDotNet.Attributes;
using OpenCvSharp;

namespace BackgroundImageRemover.Benchmarks;

/// <summary>
/// <c>CompositeOntoBgr</c> is used by every export onto a color/image/gradient background.
/// It went from ~9 intermediate CV_32F Mats to a single parallel pass over the native buffers.
/// <c>CompositeOntoColor</c> is its public entry point (color background).
/// </summary>
[MemoryDiagnoser]
public class CompositeBenchmarks
{
    private Mat _bgra = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(7);
        _bgra = new Mat(1920, 1080, MatType.CV_8UC4);
        var span = _bgra.AsSpan2D<Vec4b>();
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                span[y, x] = new Vec4b((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _bgra.Dispose();

    [Benchmark]
    public Mat CompositeOntoColor() => BackgroundCompositingService.CompositeOntoColor(_bgra, new Vec3b(30, 40, 50));

    [Benchmark]
    public Mat CompositeOntoGradient() => BackgroundCompositingService.CompositeOntoGradient(_bgra, new Vec3b(10, 20, 30), new Vec3b(200, 180, 160));
}
