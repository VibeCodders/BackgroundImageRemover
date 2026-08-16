using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
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
        // Clone to force a continuous buffer: a sub-Mat ROI is generally non-continuous
        // (its row stride matches the parent, not the ROI width), which GetArray requires.
        using var roi = new Mat(bgr, band);
        using var contiguous = roi.Clone();
        contiguous.GetArray(out Vec3b[] pixels);
        into.AddRange(pixels);
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

        var mask = new Mat(bgr.Size(), MatType.CV_8UC1);
        lab.GetArray(out Vec3b[] labPixels);
        var maskPixels = new byte[labPixels.Length];

        for (int i = 0; i < labPixels.Length; i++)
        {
            double dl = labPixels[i].Item0 - keyLabColor.Item0;
            double da = labPixels[i].Item1 - keyLabColor.Item1;
            double db = labPixels[i].Item2 - keyLabColor.Item2;
            double distance = Math.Sqrt(dl * dl + da * da + db * db);

            double t = (distance - cutoff) / FeatherBand;
            t = Math.Clamp(t, 0.0, 1.0);
            double smooth = t * t * (3 - 2 * t); // smoothstep
            maskPixels[i] = (byte)(smooth * 255);
        }

        mask.SetArray(maskPixels);
        return mask;
    }

    protected override void PostProcessBgra(Mat bgra, StrategyContext context)
    {
        if (!context.ChromaKeySpillSuppression)
        {
            return;
        }

        Vec3b keyColor;
        if (context.ChromaKeyColor is { } explicitColor)
        {
            keyColor = explicitColor;
        }
        else
        {
            var channels = Cv2.Split(bgra);
            try
            {
                using var bgr = new Mat();
                Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
                keyColor = DetectDominantBorderColor(bgr);
            }
            finally
            {
                foreach (var c in channels) c.Dispose();
            }
        }

        ColorSpillSuppressor.Suppress(bgra, keyColor);
    }
}
