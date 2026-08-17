using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Removes the original background color's cast ("spill") from the semi-transparent edge
/// pixels of a cutout. Strategy masks are feathered, so edge pixels keep the source image's
/// RGB even where the alpha is partial; without this step the old background shows up as a
/// colored halo once the cutout is composited over a new background.
/// </summary>
public static class ColorDecontaminator
{
    /// <summary>Default neighborhood radius (px) over which the background color is estimated.</summary>
    public const int DefaultEstimateRadius = 15; // matches the original fixed 31x31 kernel
    private const float DensityThreshold = 1e-4f;

    /// <summary>
    /// Decontaminates <paramref name="bgra"/> in place. When <paramref name="knownBackground"/> is
    /// supplied (chroma key), the background color is known exactly and the key's alpha is a soft
    /// key rather than true coverage, so a full unspill is unreliable; instead the dominant
    /// background channel is neutralized (classic spill suppression). Otherwise the background
    /// color is estimated per pixel from the surrounding fully-transparent pixels (within
    /// <paramref name="estimateRadius"/> pixels) and the pure foreground color is recovered as
    /// F = (C - (1-a)*B) / a.
    /// </summary>
    public static void Decontaminate(Mat bgra, Vec3b? knownBackground, int estimateRadius = DefaultEstimateRadius)
    {
        if (bgra.Channels() != 4)
        {
            return;
        }

        var channels = Cv2.Split(bgra);
        try
        {
            if (knownBackground is { } kb)
            {
                Despill(channels, kb);
            }
            else
            {
                Unspill(channels, estimateRadius);
            }

            Cv2.Merge(channels, bgra);
        }
        finally
        {
            foreach (var c in channels)
            {
                c.Dispose();
            }
        }
    }

    /// <summary>
    /// Classic chroma-key spill suppression: on semi-transparent edge pixels, pull the key
    /// color's dominant channel toward the average of the other two, weighted by how much of
    /// the pixel is still background (1 - alpha). This removes the green/blue cast without
    /// assuming the key's alpha equals the true subject coverage.
    /// </summary>
    private static void Despill(Mat[] channels, Vec3b keyColor)
    {
        int dominant = keyColor.Item0 >= keyColor.Item1 && keyColor.Item0 >= keyColor.Item2 ? 0
            : keyColor.Item1 >= keyColor.Item2 ? 1 : 2;

        // Only the semi-transparent edge pixels can carry spill; crop every operation to their
        // bounding box so a thin edge band costs a tiny fraction of a full-image pass.
        var band = FindEdgeBand(channels[3], out var edgeMask);
        using (edgeMask)
        {
            if (band is null)
            {
                return;
            }

            using var alphaView = new Mat(channels[3], band.Value);
            using var edgeView = new Mat(edgeMask, band.Value);
            using var dominantView = new Mat(channels[dominant], band.Value);

            using var alphaF = new Mat();
            alphaView.ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

            using var c0 = new Mat();
            using var c1 = new Mat();
            using var c2 = new Mat();
            using (var v0 = new Mat(channels[0], band.Value)) v0.ConvertTo(c0, MatType.CV_32FC1);
            using (var v1 = new Mat(channels[1], band.Value)) v1.ConvertTo(c1, MatType.CV_32FC1);
            using (var v2 = new Mat(channels[2], band.Value)) v2.ConvertTo(c2, MatType.CV_32FC1);

            var dominantF = dominant switch { 0 => c0, 1 => c1, _ => c2 };

            // Average of the two non-dominant channels: (c0 + c1 + c2 - dominant) / 2.
            using var sum = new Mat();
            Cv2.Add(c0, c1, sum);
            Cv2.Add(sum, c2, sum);
            using var othersAvg = new Mat();
            Cv2.Subtract(sum, dominantF, othersAvg);
            Cv2.Multiply(othersAvg, Scalar.All(0.5), othersAvg);

            // Excess of the dominant channel over the others, clamped at zero.
            using var delta = new Mat();
            Cv2.Subtract(dominantF, othersAvg, delta);
            using var spill = new Mat();
            Cv2.Max(delta, Scalar.All(0.0), spill);

            // new dominant = dominant - spill * (1 - alpha).
            using var w = new Mat();
            Cv2.Subtract(new Mat(alphaF.Size(), alphaF.Type(), Scalar.All(1.0)), alphaF, w);
            using var newDominant = new Mat();
            Cv2.Multiply(spill, w, newDominant);
            Cv2.Subtract(dominantF, newDominant, newDominant);

            // Apply only where the pixel is semi-transparent AND the dominant channel is actually
            // elevated above the others.
            using var dominantMask = new Mat();
            Cv2.Compare(dominantF, othersAvg, dominantMask, CmpType.GT);
            using var apply = new Mat();
            Cv2.BitwiseAnd(edgeView, dominantMask, apply);

            using var newDominant8 = new Mat();
            newDominant.ConvertTo(newDominant8, MatType.CV_8UC1);
            newDominant8.CopyTo(dominantView, apply);
        }
    }

    /// <summary>
    /// Matte-based decontamination: recovers the pure foreground color F = (C - (1-a)*B) / a
    /// for every 0 &lt; a &lt; 1 pixel, with B estimated per pixel from surrounding transparent pixels.
    /// Valid for strategies whose feathered alpha approximates the true subject coverage.
    /// </summary>
    private static void Unspill(Mat[] channels, int estimateRadius)
    {
        // The estimate needs a neighborhood, so the working region is the edge band expanded by
        // the estimate radius; the box-filter windows of every band pixel stay inside it.
        var band = FindEdgeBand(channels[3], out var edgeMask);
        using (edgeMask)
        {
            if (band is null)
            {
                return;
            }

            var roi = ExpandRect(band.Value, estimateRadius, channels[0].Size());

            using var alphaView = new Mat(channels[3], roi);
            using var edgeView = new Mat(edgeMask, roi);
            using var alphaF = new Mat();
            alphaView.ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0);

            var views = new Mat[3];
            for (int c = 0; c < 3; c++)
            {
                views[c] = new Mat(channels[c], roi);
            }

            try
            {
                var (bgB, bgG, bgR, density) = EstimateBackground(views, alphaView, estimateRadius);
                using (bgB)
                using (bgG)
                using (bgR)
                using (density)
                {
                    // Pixels without a reliable local background estimate are left untouched.
                    using var densityMask = new Mat();
                    Cv2.Compare(density, Scalar.All(DensityThreshold), densityMask, CmpType.GE);
                    using var valid = new Mat();
                    Cv2.BitwiseAnd(edgeView, densityMask, valid);

                    using var w = new Mat();
                    Cv2.Subtract(new Mat(alphaF.Size(), alphaF.Type(), Scalar.All(1.0)), alphaF, w);

                    var backgrounds = new[] { bgB, bgG, bgR };
                    for (int c = 0; c < 3; c++)
                    {
                        using var channelF = new Mat();
                        views[c].ConvertTo(channelF, MatType.CV_32FC1);

                        using var weighted = new Mat();
                        Cv2.Multiply(w, backgrounds[c], weighted);   // (1 - a) * B

                        using var numerator = new Mat();
                        Cv2.Subtract(channelF, weighted, numerator); // C - (1 - a) * B

                        using var pure = new Mat();
                        Cv2.Divide(numerator, alphaF, pure);         // / a

                        using var pure8 = new Mat();
                        pure.ConvertTo(pure8, MatType.CV_8UC1);      // saturate + round to [0, 255]
                        pure8.CopyTo(views[c], valid);               // writes through the view
                    }
                }
            }
            finally
            {
                foreach (var v in views)
                {
                    v.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Bounding box of the semi-transparent (1..254 alpha) pixels, with the corresponding
    /// binary mask; null when there is nothing to decontaminate.
    /// </summary>
    private static Rect? FindEdgeBand(Mat alpha, out Mat edgeMask)
    {
        edgeMask = new Mat();
        Cv2.InRange(alpha, new Scalar(1), new Scalar(254), edgeMask);

        using var nonZero = new Mat();
        Cv2.FindNonZero(edgeMask, nonZero);
        if (nonZero.Rows == 0)
        {
            edgeMask.Dispose();
            edgeMask = null!;
            return null;
        }

        return Cv2.BoundingRect(nonZero);
    }

    /// <summary>Grows <paramref name="rect"/> by <paramref name="margin"/> pixels, clamped to the image.</summary>
    private static Rect ExpandRect(Rect rect, int margin, Size bounds)
    {
        int x = Math.Max(0, rect.X - margin);
        int y = Math.Max(0, rect.Y - margin);
        int right = Math.Min(bounds.Width, rect.X + rect.Width + margin);
        int bottom = Math.Min(bounds.Height, rect.Y + rect.Height + margin);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    /// <summary>
    /// Estimates the background color at every pixel of the working region, returning BGR float
    /// Mats plus the estimation density. The estimate is a low-frequency field (a box filter of
    /// the mask), so on large regions it is computed at reduced resolution and upsampled back;
    /// small regions are computed exactly.
    /// </summary>
    private static (Mat B, Mat G, Mat R, Mat Density) EstimateBackground(Mat[] channelViews, Mat alphaView, int estimateRadius)
    {
        // Fully transparent (alpha == 0) pixels are known background; everything else is excluded.
        using var bgMask = new Mat();
        Cv2.Threshold(alphaView, bgMask, 0, 255, ThresholdTypes.BinaryInv);
        using var maskF = new Mat();
        bgMask.ConvertTo(maskF, MatType.CV_32FC1, 1.0 / 255.0);

        int kernelSize = Math.Max(3, estimateRadius * 2 + 1);

        // Downscale so the low-resolution kernel stays at least 3x3, but never below 1/8 and
        // only when the region is genuinely large (small images stay pixel-exact).
        int scale = Math.Min(8, Math.Max(1, kernelSize / 3));
        bool downscale = scale > 1
            && alphaView.Width / scale >= 128
            && alphaView.Height / scale >= 128;

        using var workMask = new Mat();
        Size workSize = alphaView.Size();
        Size workKernel = new(kernelSize, kernelSize);
        if (downscale)
        {
            workSize = new Size(alphaView.Width / scale, alphaView.Height / scale);
            Cv2.Resize(maskF, workMask, workSize, interpolation: InterpolationFlags.Area);
            int smallKernel = Math.Max(3, kernelSize / scale);
            workKernel = new Size(smallKernel, smallKernel);
        }
        else
        {
            maskF.CopyTo(workMask);
        }

        // Normalized box filter of the mask = local fraction of background pixels (0..1).
        var density = new Mat();
        Cv2.BoxFilter(workMask, density, MatType.CV_32F, workKernel);

        // Local mean background color per channel = box(channel*mask) / box(mask).
        // Where the density is ~0 the division yields NaN/Inf, but those pixels are masked out later.
        var result = new Mat[3];
        for (int c = 0; c < 3; c++)
        {
            using var channelF = new Mat();
            if (downscale)
            {
                using var small = new Mat();
                Cv2.Resize(channelViews[c], small, workSize, interpolation: InterpolationFlags.Area);
                small.ConvertTo(channelF, MatType.CV_32FC1);
            }
            else
            {
                channelViews[c].ConvertTo(channelF, MatType.CV_32FC1);
            }

            using var weighted = channelF.Mul(workMask).ToMat();
            var numerator = new Mat();
            Cv2.BoxFilter(weighted, numerator, MatType.CV_32F, workKernel);
            var estimate = new Mat();
            Cv2.Divide(numerator, density, estimate);
            numerator.Dispose();

            if (downscale)
            {
                var fullSize = new Mat();
                Cv2.Resize(estimate, fullSize, alphaView.Size(), interpolation: InterpolationFlags.Linear);
                estimate.Dispose();
                estimate = fullSize;
            }

            result[c] = estimate;
        }

        if (downscale)
        {
            var fullDensity = new Mat();
            Cv2.Resize(density, fullDensity, alphaView.Size(), interpolation: InterpolationFlags.Linear);
            density.Dispose();
            density = fullDensity;
        }

        return (result[0], result[1], result[2], density);
    }
}