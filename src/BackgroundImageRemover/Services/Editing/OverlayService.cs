using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Composites a second image (logo/sticker) over a BGR base with scale, opacity and position.</summary>
public static class OverlayService
{
    public static Mat Composite(Mat baseBgr, Mat overlayBgra, TextAnchor anchor, double scale, double opacity, int margin)
    {
        scale = Math.Max(0.01, scale);
        opacity = Math.Clamp(opacity, 0.0, 1.0);
        margin = Math.Max(0, margin);

        using var resized = new Mat();
        Cv2.Resize(overlayBgra, resized, new Size(0, 0), scale, scale, InterpolationFlags.Lanczos4);

        var (pos, overlayOffset) = ComputeIntersection(baseBgr.Size(), resized.Size(), anchor, margin);
        if (pos.Width <= 0 || pos.Height <= 0)
        {
            return baseBgr.Clone();
        }

        var result = baseBgr.Clone();
        using var roi = new Mat(result, pos);
        using var overlayRoi = new Mat(resized, overlayOffset);

        // Alpha-blend the overlay (BGRA) over the base (BGR).
        var channels = Cv2.Split(overlayRoi);
        try
        {
            using var alphaF = new Mat();
            channels[3].ConvertTo(alphaF, MatType.CV_32FC1, 1.0 / 255.0 * opacity);

            using var overlayBgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, overlayBgr);

            using var overlayF = new Mat();
            overlayBgr.ConvertTo(overlayF, MatType.CV_32FC3);
            using var baseF = new Mat();
            roi.ConvertTo(baseF, MatType.CV_32FC3);

            using var alpha3 = new Mat();
            Cv2.CvtColor(alphaF, alpha3, ColorConversionCodes.GRAY2BGR);

            using var fgWeighted = overlayF.Mul(alpha3).ToMat();
            using var oneMinus = new Mat();
            Cv2.Subtract(new Mat(alpha3.Size(), alpha3.Type(), Scalar.All(1.0)), alpha3, oneMinus);
            using var bgWeighted = baseF.Mul(oneMinus).ToMat();

            using var blended = (fgWeighted + bgWeighted).ToMat();
            blended.ConvertTo(roi, MatType.CV_8UC3);
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }

        return result;
    }

    private static (Rect Destination, Rect Overlay) ComputeIntersection(Size baseSize, Size overlaySize, TextAnchor anchor, int margin)
    {
        int x = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.MiddleLeft or TextAnchor.BottomLeft => margin,
            TextAnchor.TopCenter or TextAnchor.Center or TextAnchor.BottomCenter => (baseSize.Width - overlaySize.Width) / 2,
            _ => baseSize.Width - overlaySize.Width - margin
        };
        int y = anchor switch
        {
            TextAnchor.TopLeft or TextAnchor.TopCenter or TextAnchor.TopRight => margin,
            TextAnchor.MiddleLeft or TextAnchor.Center or TextAnchor.MiddleRight => (baseSize.Height - overlaySize.Height) / 2,
            _ => baseSize.Height - overlaySize.Height - margin
        };

        int ix0 = Math.Max(0, x);
        int iy0 = Math.Max(0, y);
        int ix1 = Math.Min(baseSize.Width, x + overlaySize.Width);
        int iy1 = Math.Min(baseSize.Height, y + overlaySize.Height);

        return (
            new Rect(ix0, iy0, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)),
            new Rect(ix0 - x, iy0 - y, Math.Max(0, ix1 - ix0), Math.Max(0, iy1 - iy0)));
    }
}
