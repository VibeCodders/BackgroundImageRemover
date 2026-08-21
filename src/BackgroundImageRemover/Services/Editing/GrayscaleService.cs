using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class GrayscaleService
{
    public static Mat ToGrayscale(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return input.Clone();
        }

        using var gray = new Mat();
        Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);

        using var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);

        if (strength >= 1)
        {
            return grayBgr;
        }

        var result = new Mat();
        Cv2.AddWeighted(input, 1 - strength, grayBgr, strength, 0, result);
        return result;
    }
}
