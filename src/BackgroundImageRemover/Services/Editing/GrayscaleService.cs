using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class GrayscaleService
{
    public static Mat ToGrayscale(Mat input, double strength)
    {
        if (input is null || input.Empty())
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(input);
        }

        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);

        using var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);

        return ImageProcessingUtility.BlendLinear(input, grayBgr, strength);
    }
}
