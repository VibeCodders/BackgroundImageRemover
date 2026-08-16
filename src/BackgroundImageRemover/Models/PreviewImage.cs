using OpenCvSharp;

namespace BackgroundImageRemover.Models;

/// <summary>Downscaled version of the source image used for fast, interactive previews.</summary>
public sealed class PreviewImage : IDisposable
{
    public Mat Bgr { get; }

    /// <summary>Factor to multiply preview-space coordinates by to get full-image-space coordinates.</summary>
    public double ScaleFactor { get; }

    public PreviewImage(Mat bgr, double scaleFactor)
    {
        Bgr = bgr;
        ScaleFactor = scaleFactor;
    }

    public void Dispose() => Bgr.Dispose();
}
