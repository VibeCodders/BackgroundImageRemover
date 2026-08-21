using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Selective and whole-image blur operations for the Blur tool.</summary>
public static class BlurService
{
    /// <summary>
    /// Blurs a painted (non-zero) region of <paramref name="mask"/> with a Gaussian blur of
    /// <paramref name="radius"/> pixels, leaving the rest of the image untouched.
    /// The blur bleeds across the mask boundary, so the transition is naturally soft.
    /// </summary>
    public static Mat BlurRegion(Mat bgr, Mat mask, double radius)
    {
        radius = Math.Max(0.5, radius);
        int ksize = ImageProcessingUtility.GaussianKernelSize(radius);

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(ksize, ksize), radius, radius);

        return bgr.BlendByMask(blurred, mask);
    }

    /// <summary>Blurs the entire image with a Gaussian blur of <paramref name="radius"/> pixels.</summary>
    public static Mat BlurAll(Mat bgr, double radius)
    {
        radius = Math.Max(0.5, radius);
        int ksize = ImageProcessingUtility.GaussianKernelSize(radius);
        var result = new Mat();
        Cv2.GaussianBlur(bgr, result, new Size(ksize, ksize), radius, radius);
        return result;
    }

    /// <summary>
    /// Motion blur in the given <paramref name="angleDeg"/> (degrees, 0 = horizontal) with
    /// <paramref name="length"/> pixels.
    /// </summary>
    public static Mat MotionBlur(Mat bgr, double length, double angleDeg)
    {
        length = Math.Max(1, length);
        int ksize = (int)Math.Round(length);
        if (ksize % 2 == 0) ksize++;

        var kernel = new Mat(ksize, ksize, MatType.CV_32FC1, Scalar.All(0));
        double rad = angleDeg * Math.PI / 180.0;
        double dx = Math.Cos(rad);
        double dy = Math.Sin(rad);
        int cx = ksize / 2;
        int cy = ksize / 2;
        for (int i = 0; i < ksize; i++)
        {
            int x = cx + (int)Math.Round(dx * (i - ksize / 2));
            int y = cy + (int)Math.Round(dy * (i - ksize / 2));
            if (x >= 0 && x < ksize && y >= 0 && y < ksize)
            {
                kernel.Set(y, x, 1.0 / ksize);
            }
        }

        var result = new Mat();
        Cv2.Filter2D(bgr, result, -1, kernel);
        return result;
    }
}
