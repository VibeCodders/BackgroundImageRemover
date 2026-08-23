using BackgroundImageRemover.Helpers;
using BenchmarkDotNet.Attributes;
using OpenCvSharp;

namespace BackgroundImageRemover.Benchmarks;

/// <summary>
/// Parallel row processing (<see cref="PixelLoop.ForEachRowParallel"/>) vs the sequential pass
/// it replaced, using a Hue/Sat-style saturation multiply on a CV_8UC3 buffer (1920×1080).
/// Rows are independent, so both produce identical output.
/// </summary>
[MemoryDiagnoser]
public class PixelLoopBenchmarks
{
    private Mat _mat = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(99);
        _mat = new Mat(1920, 1080, MatType.CV_8UC3);
        var span = _mat.AsSpan2D<Vec3b>();
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                span[y, x] = new Vec3b((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _mat.Dispose();

    [Benchmark(Baseline = true)]
    public void SequentialRows()
    {
        var span = _mat.AsSpan2D<Vec3b>();
        for (int y = 0; y < span.Height; y++)
        {
            for (int x = 0; x < span.Width; x++)
            {
                var px = span[y, x];
                span[y, x] = new Vec3b(
                    px.Item0,
                    (byte)Math.Clamp(px.Item1 * 1.3, 0, 255),
                    (byte)Math.Clamp(px.Item2 * 0.8, 0, 255));
            }
        }
    }

    [Benchmark]
    public unsafe void ParallelRows()
    {
        PixelLoop.ForEachRowParallel(_mat, (rowPtr, _) =>
        {
            var row = new Span<Vec3b>((Vec3b*)rowPtr, _mat.Cols);
            for (int x = 0; x < row.Length; x++)
            {
                var px = row[x];
                row[x] = new Vec3b(
                    px.Item0,
                    (byte)Math.Clamp(px.Item1 * 1.3, 0, 255),
                    (byte)Math.Clamp(px.Item2 * 0.8, 0, 255));
            }
        });
    }
}
