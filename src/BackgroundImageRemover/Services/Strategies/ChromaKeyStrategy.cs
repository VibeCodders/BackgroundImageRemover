using System.Threading.Tasks;
using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Strategies;

/// <summary>
/// Removes a background of roughly-uniform color. The key color is auto-detected from
/// the image's border pixels; distance is measured in Lab space for perceptual accuracy,
/// with a smoothstep feather band at the tolerance boundary for an anti-aliased edge.
/// </summary>
public sealed class ChromaKeyStrategy : StrategyBase
{
    public override StrategyKind Kind => StrategyKind.ChromaKey;

    private const int BorderBandFractionDenominator = 20; // ~5% band
    private const int HistogramBinsPerChannel = 16;
    private const double FeatherBand = 8.0; // Lab-distance units over which the mask fades

    /// <summary>Detects the dominant border color of <paramref name="bgr"/> via a quantized histogram.</summary>
    public static Vec3b DetectDominantBorderColor(Mat bgr)
    {
        if (bgr.Width <= 0 || bgr.Height <= 0)
        {
            return default; // empty image: no border to sample, avoid the At(0,0) fallback crash
        }

        int bandW = Math.Max(1, bgr.Width / BorderBandFractionDenominator);
        int bandH = Math.Max(1, bgr.Height / BorderBandFractionDenominator);

        var samples = new List<Vec3b>();
        CollectBand(bgr, new Rect(0, 0, bgr.Width, bandH), samples);
        CollectBand(bgr, new Rect(0, bgr.Height - bandH, bgr.Width, bandH), samples);
        CollectBand(bgr, new Rect(0, 0, bandW, bgr.Height), samples);
        CollectBand(bgr, new Rect(bgr.Width - bandW, 0, bandW, bgr.Height), samples);

        if (samples.Count == 0)
        {
            return bgr.At<Vec3b>(0, 0);
        }

        var bins = new Dictionary<(int, int, int), (long sumB, long sumG, long sumR, int count)>();
        foreach (var px in samples)
        {
            var key = (px.Item0 / HistogramBinsPerChannel, px.Item1 / HistogramBinsPerChannel, px.Item2 / HistogramBinsPerChannel);
            bins.TryGetValue(key, out var acc);
            bins[key] = (acc.sumB + px.Item0, acc.sumG + px.Item1, acc.sumR + px.Item2, acc.count + 1);
        }

        var best = bins.Values.OrderByDescending(v => v.count).First();
        return new Vec3b((byte)(best.sumB / best.count), (byte)(best.sumG / best.count), (byte)(best.sumR / best.count));
    }

    private static void CollectBand(Mat bgr, Rect band, List<Vec3b> into)
    {
        // Read the band straight from the parent buffer: the row pointers honor the parent's
        // stride, so no ROI clone + GetArray copy is needed.
        int cols = band.Width;
        int rows = band.Height;
        unsafe
        {
            byte* basePtr = (byte*)bgr.DataPointer + (long)band.Y * bgr.Step() + (long)band.X * 3;
            long step = bgr.Step();
            for (int y = 0; y < rows; y++)
            {
                var row = new Span<Vec3b>((Vec3b*)(basePtr + y * step), cols);
                for (int x = 0; x < cols; x++)
                {
                    into.Add(row[x]);
                }
            }
        }
    }

    protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
    {
        var keyColor = context.ChromaKeyColor ?? DetectDominantBorderColor(bgr);
        // Tolerance (0-100) maps directly to a Lab-space distance cutoff, which covers the
        // useful range for perceptual color difference.
        double cutoff = Math.Max(0.1, context.ChromaKeyTolerance);

        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);

        using var keyMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(keyColor.Item0, keyColor.Item1, keyColor.Item2));
        using var keyLab = new Mat();
        Cv2.CvtColor(keyMat, keyLab, ColorConversionCodes.BGR2Lab);
        var keyLabColor = keyLab.At<Vec3b>(0, 0);

        // Lab-distance per pixel is independent, so the mask is written straight into the
        // native buffer in parallel — no GetArray/SetArray copies, and the sqrt math runs on
        // all cores. Math is identical to the sequential version.
        var mask = new Mat(bgr.Size(), MatType.CV_8UC1);
        int w = bgr.Width;
        unsafe
        {
            byte* labPtr = (byte*)lab.DataPointer;
            byte* maskPtr = (byte*)mask.DataPointer;
            long labStep = lab.Step();
            long maskStep = mask.Step();
            Parallel.For(0, bgr.Rows, y =>
            {
                var labRow = new Span<Vec3b>((Vec3b*)(labPtr + y * labStep), w);
                var maskRow = new Span<byte>((byte*)(maskPtr + y * maskStep), w);
                for (int x = 0; x < w; x++)
                {
                    var px = labRow[x];
                    double dl = px.Item0 - keyLabColor.Item0;
                    double da = px.Item1 - keyLabColor.Item1;
                    double db = px.Item2 - keyLabColor.Item2;
                    double distance = Math.Sqrt(dl * dl + da * da + db * db);

                    double t = (distance - cutoff) / FeatherBand;
                    t = Math.Clamp(t, 0.0, 1.0);
                    double smooth = t * t * (3 - 2 * t); // smoothstep
                    maskRow[x] = (byte)(smooth * 255);
                }
            });
        }
        return mask;
    }
}
