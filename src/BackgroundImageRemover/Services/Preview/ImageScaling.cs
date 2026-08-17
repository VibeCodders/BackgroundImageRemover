using OpenCvSharp;

namespace BackgroundImageRemover.Services.Preview;

/// <summary>Shared "fit within a max dimension, preserving aspect ratio" resize math.</summary>
public static class ImageScaling
{
    public static Size ComputeFitSize(int width, int height, int maxDim)
    {
        double scale = (double)maxDim / Math.Max(width, height);
        int newWidth = Math.Max(1, (int)Math.Round(width * scale));
        int newHeight = Math.Max(1, (int)Math.Round(height * scale));
        return new Size(newWidth, newHeight);
    }
}
