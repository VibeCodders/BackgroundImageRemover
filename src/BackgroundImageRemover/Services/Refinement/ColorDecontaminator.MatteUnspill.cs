using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Matte-based decontamination operations.
/// </summary>
internal static class MatteUnspill
{
    /// <summary>
    /// Matte-based decontamination: recovers the pure foreground color F = (C - (1-a)*B) / a
    /// for every 0 &lt; a &lt; 1 pixel, with B estimated per pixel from surrounding transparent pixels.
    /// Valid for strategies whose feathered alpha approximates the true subject coverage.
    /// </summary>
    public static void Unspill(Mat[] channels, int estimateRadius)
    {
        // The estimate needs a neighborhood, so the working region is the edge band expanded by
        // the estimate radius; the box-filter windows of every band pixel stay inside it.
        var band = EdgeDetection.FindEdgeBand(channels[3], out var edgeMask);
        using (edgeMask)
        {
            if (band is null)
            {
                return;
            }

            var roi = EdgeDetection.ExpandRect(band.Value, estimateRadius, channels[0].Size());

            using var alphaView = new Mat(channels[3], roi);
            using var edgeView = new Mat(edgeMask, roi);
            using var alphaF = ImageProcessingUtility.Gray8ToFloat01(alphaView);

            var views = new Mat[3];
            for (int c = 0; c < 3; c++)
            {
                views[c] = new Mat(channels[c], roi);
            }

            try
            {
                var (bgB, bgG, bgR, density) = BackgroundEstimation.EstimateBackground(views, alphaView, estimateRadius);
                using (bgB)
                using (bgG)
                using (bgR)
                using (density)
                {
                    // Pixels without a reliable local background estimate are left untouched.
                    using var valid = BackgroundEstimation.CreateDensityMask(density, edgeView);

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
}