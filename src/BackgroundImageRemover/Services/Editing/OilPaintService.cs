using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Oil-paint effect: each pixel takes the average color of the most common
/// intensity bin inside its neighbourhood (the classic "dominant colour" algorithm).</summary>
public static class OilPaintService
{
    /// <summary>
    /// Applies an oil-painting effect to <paramref name="bgr"/>. <paramref name="radius"/> (1..10)
    /// is the brush neighbourhood size and <paramref name="levels"/> (2..32) the number of
    /// intensity bins used to pick the dominant colour. Implemented with box filters so it stays
    /// fast at any radius. The caller owns the returned Mat.
    /// </summary>
    public static Mat Apply(Mat bgr, int radius, int levels)
    {
        radius = Math.Clamp(radius, 1, 10);
        levels = Math.Clamp(levels, 2, 32);

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        // binIdx[y,x] = floor(gray * levels / 256), i.e. an intensity bucket in 0..levels-1.
        using var grayF = new Mat();
        gray.ConvertTo(grayF, MatType.CV_32FC1);
        using var scaled = new Mat();
        Cv2.Multiply(grayF, levels / 256.0, scaled);
        using var binIdx = new Mat();
        scaled.ConvertTo(binIdx, MatType.CV_32SC1);

        int k = radius * 2 + 1;
        var kernel = new OpenCvSharp.Size(k, k);

        // Per-bin: pixel count and per-channel colour sums via box filters.
        var counts = new Mat[levels];
        var sumsB = new Mat[levels];
        var sumsG = new Mat[levels];
        var sumsR = new Mat[levels];
        var masks = new Mat[levels];
        try
        {
            for (int b = 0; b < levels; b++)
            {
                masks[b] = new Mat();
                Cv2.Compare(binIdx, b, masks[b], CmpType.EQ);
                masks[b].ConvertTo(masks[b], MatType.CV_32FC1);

                counts[b] = new Mat();
                Cv2.BoxFilter(masks[b], counts[b], MatType.CV_32FC1, kernel,
                    anchor: new Point(-1, -1), normalize: false, BorderTypes.Replicate);

                using var mb = new Mat();
                using var mg = new Mat();
                using var mr = new Mat();
                Cv2.ExtractChannel(bgr, mb, 0);
                Cv2.ExtractChannel(bgr, mg, 1);
                Cv2.ExtractChannel(bgr, mr, 2);
                mb.ConvertTo(mb, MatType.CV_32FC1);
                mg.ConvertTo(mg, MatType.CV_32FC1);
                mr.ConvertTo(mr, MatType.CV_32FC1);
                Cv2.Multiply(mb, masks[b], mb);
                Cv2.Multiply(mg, masks[b], mg);
                Cv2.Multiply(mr, masks[b], mr);

                sumsB[b] = new Mat();
                sumsG[b] = new Mat();
                sumsR[b] = new Mat();
                Cv2.BoxFilter(mb, sumsB[b], MatType.CV_32FC1, kernel,
                    anchor: new Point(-1, -1), normalize: false, BorderTypes.Replicate);
                Cv2.BoxFilter(mg, sumsG[b], MatType.CV_32FC1, kernel,
                    anchor: new Point(-1, -1), normalize: false, BorderTypes.Replicate);
                Cv2.BoxFilter(mr, sumsR[b], MatType.CV_32FC1, kernel,
                    anchor: new Point(-1, -1), normalize: false, BorderTypes.Replicate);
            }

            // Pick the dominant bin per pixel and take its average colour.
            var result = new Mat(bgr.Size(), MatType.CV_8UC3);
            PixelLoop.ForEach(bgr, (y, x) =>
            {
                int best = 0;
                float bestCount = counts[0].At<float>(y, x);
                for (int b = 1; b < levels; b++)
                {
                    float c = counts[b].At<float>(y, x);
                    if (c > bestCount)
                    {
                        bestCount = c;
                        best = b;
                    }
                }

                float cb = sumsB[best].At<float>(y, x);
                float cg = sumsG[best].At<float>(y, x);
                float cr = sumsR[best].At<float>(y, x);
                var src = bgr.At<Vec3b>(y, x);
                byte vb = bestCount > 0 ? (byte)Math.Clamp(cb / bestCount, 0, 255) : src.Item0;
                byte vg = bestCount > 0 ? (byte)Math.Clamp(cg / bestCount, 0, 255) : src.Item1;
                byte vr = bestCount > 0 ? (byte)Math.Clamp(cr / bestCount, 0, 255) : src.Item2;
                result.Set<Vec3b>(y, x, new Vec3b(vb, vg, vr));
            });

            return result;
        }
        finally
        {
            for (int b = 0; b < levels; b++)
            {
                masks[b]?.Dispose();
                counts[b]?.Dispose();
                sumsB[b]?.Dispose();
                sumsG[b]?.Dispose();
                sumsR[b]?.Dispose();
            }
        }
    }
}
