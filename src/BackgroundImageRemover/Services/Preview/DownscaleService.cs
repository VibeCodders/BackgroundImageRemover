using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Preview;

public interface IDownscaleService
{
    PreviewImage CreatePreview(Mat full, int maxDim = 800);
}

public sealed class DownscaleService : IDownscaleService
{
    public PreviewImage CreatePreview(Mat full, int maxDim = 800)
    {
        int longestSide = Math.Max(full.Width, full.Height);
        if (longestSide <= maxDim)
        {
            return new PreviewImage(full.Clone(), 1.0);
        }

        var size = ImageScaling.ComputeFitSize(full.Width, full.Height, maxDim);
        var resized = new Mat();
        Cv2.Resize(full, resized, size, interpolation: InterpolationFlags.Area);

        // ScaleFactor converts preview-space coords to full-image-space coords.
        double scaleFactor = (double)longestSide / maxDim;
        return new PreviewImage(resized, scaleFactor);
    }
}
