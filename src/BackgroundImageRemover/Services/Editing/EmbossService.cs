using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class EmbossService
{
    public static Mat Emboss(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(input);
        }

        using var kernel = new Mat(3, 3, MatType.CV_64F);
        kernel.Set<double>(0, 0, -2);
        kernel.Set<double>(0, 1, -1);
        kernel.Set<double>(0, 2, 0);
        kernel.Set<double>(1, 0, -1);
        kernel.Set<double>(1, 1, 1);
        kernel.Set<double>(1, 2, 1);
        kernel.Set<double>(2, 0, 0);
        kernel.Set<double>(2, 1, 1);
        kernel.Set<double>(2, 2, 2);

        using var filtered = new Mat();
        Cv2.Filter2D(input, filtered, MatType.CV_16S, kernel);
        using var typed = new Mat();
        filtered.ConvertTo(typed, input.Type());

        return ImageProcessingUtility.BlendLinear(input, typed, strength);
    }
}
