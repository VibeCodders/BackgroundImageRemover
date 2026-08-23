using BackgroundImageRemover.Helpers;
using BenchmarkDotNet.Attributes;

namespace BackgroundImageRemover.Benchmarks;

/// <summary>
/// The ZLinq SIMD per-pixel transforms introduced with the .NET 9 migration
/// (<see cref="ZLinqPixelOps"/>), compared against the scalar math they replace. These run on a
/// 1024² float buffer — the size of an ONNX/SAM mask plane — and must be bit-identical to the
/// scalar versions (covered by ZLinqPixelOpsTests). Each iteration restores the buffer from a
/// template because the transforms mutate it in place.
/// </summary>
public abstract class ZlinqTransformBenchmarkBase
{
    protected float[] Template = null!;
    protected float[] Values = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(123);
        Template = new float[1024 * 1024];
        for (int i = 0; i < Template.Length; i++)
        {
            Template[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        Values = (float[])Template.Clone();
    }

    [IterationSetup]
    public void ResetBuffer() => Template.CopyTo(Values, 0);
}

[MemoryDiagnoser]
public class NormalizeMaskBenchmarks : ZlinqTransformBenchmarkBase
{
    private readonly float _min = -0.4f;
    private readonly float _range = 1.6f;

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var values = Values;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Math.Clamp((values[i] - _min) / _range * 255f, 0f, 255f);
        }
    }

    [Benchmark]
    public void ZLinqSimd() => ZLinqPixelOps.NormalizeMaskToByteRange(Values, _min, _range);
}

[MemoryDiagnoser]
public class ThresholdMaskBenchmarks : ZlinqTransformBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var values = Values;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = values[i] > 0 ? 1f : 0f;
        }
    }

    [Benchmark]
    public void ZLinqSimd() => ZLinqPixelOps.ThresholdToUnit(Values);
}

[MemoryDiagnoser]
public class WrapHueBenchmarks : ZlinqTransformBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var values = Values;
        for (int i = 0; i < values.Length; i++)
        {
            float m = values[i] % 180f;
            if (m < 0)
            {
                m += 180f;
            }

            values[i] = m;
        }
    }

    [Benchmark]
    public void ZLinqSimd() => ZLinqPixelOps.WrapHue180(Values);
}
