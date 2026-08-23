using BackgroundImageRemover.Helpers;
using BenchmarkDotNet.Attributes;
using OpenCvSharp;

namespace BackgroundImageRemover.Benchmarks;

/// <summary>
/// The ONNX/SAM input normalization used to run as a full-image <c>CvtColor(BGR2RGB)</c> pass
/// followed by a parallel CHW fill reading the RGB buffer. The BGR→RGB swap is now folded into
/// the fill's channel writes, eliminating the native conversion pass entirely. Both variants
/// produce bit-identical tensors; these benchmarks quantify the eliminated pass at 1024²
/// (the MobileSAM encoder input size).
/// </summary>
[MemoryDiagnoser]
public class ChwFillBenchmarks
{
    private const int Size = 1024;
    private Mat _bgr = null!;
    private float[] _tensor = null!;
    private float[] _scale = null!;
    private float[] _offset = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(5);
        _bgr = new Mat(Size, Size, MatType.CV_8UC3);
        var span = _bgr.AsSpan2D<Vec3b>();
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                span[y, x] = new Vec3b((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
            }
        }

        _tensor = new float[3 * Size * Size];
        _scale = new[] { 1f / (255f * 0.229f), 1f / (255f * 0.224f), 1f / (255f * 0.225f) };
        _offset = new[] { 0.485f / 0.229f, 0.456f / 0.224f, 0.406f / 0.225f };
    }

    [GlobalCleanup]
    public void Cleanup() => _bgr.Dispose();

    /// <summary>The old flow: a full-image native BGR→RGB pass, then a parallel fill over the RGB buffer.</summary>
    [Benchmark(Baseline = true)]
    public unsafe void CvtColorThenFill()
    {
        using var rgb = new Mat();
        Cv2.CvtColor(_bgr, rgb, ColorConversionCodes.BGR2RGB);
        Fill(rgb, swapChannels: false);
    }

    /// <summary>The fused flow: one parallel fill reading the BGR buffer, folding the swap into the writes.</summary>
    [Benchmark]
    public unsafe void FusedFill()
    {
        Fill(_bgr, swapChannels: true);
    }

    private unsafe void Fill(Mat src, bool swapChannels)
    {
        int plane = Size * Size;
        byte* srcPtr = (byte*)src.DataPointer;
        long srcStep = src.Step();
        Parallel.For(0, Size, y =>
        {
            var row = new Span<Vec3b>((Vec3b*)(srcPtr + y * srcStep), Size);
            var tensor = _tensor;
            int i = y * Size;
            for (int x = 0; x < Size; x++)
            {
                var px = row[x];
                if (swapChannels)
                {
                    tensor[i] = px.Item2 * _scale[0] - _offset[0]; // R
                    tensor[plane + i] = px.Item1 * _scale[1] - _offset[1]; // G
                    tensor[2 * plane + i] = px.Item0 * _scale[2] - _offset[2]; // B
                }
                else
                {
                    tensor[i] = px.Item0 * _scale[0] - _offset[0];
                    tensor[plane + i] = px.Item1 * _scale[1] - _offset[1];
                    tensor[2 * plane + i] = px.Item2 * _scale[2] - _offset[2];
                }

                i++;
            }
        });
    }
}
