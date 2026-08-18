using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Chroma-key spill suppression operations.
/// </summary>
internal static class ChromaKeyDespill
{
    /// <summary>
    /// Classic chroma-key spill suppression: on semi-transparent edge pixels, pull the key
    /// color's dominant channel toward the average of the other two, weighted by how much of
    /// the pixel is still background (1 - alpha). This removes the green/blue cast without
    /// assuming the key's alpha equals the true subject coverage.
    /// </summary>
    public static void Despill(Mat[] channels, Vec3b keyColor)
    {
        int dominant = keyColor.Item0 >= keyColor.Item1 && keyColor.Item0 >= keyColor.Item2 ? 0
            : keyColor.Item1 >= keyColor.Item2 ? 1 : 2;

        // Only the semi-transparent edge pixels can carry spill; crop every operation to their
        // bounding box so a thin edge band costs a tiny fraction of a full-image pass.
        var band = EdgeDetection.FindEdgeBand(channels[3], out var edgeMask);
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
}