using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class HueSatService
{
    public static Mat AdjustHueSat(Mat bgr, double hueShift, double satMult, double valMult)
    {
        if (bgr is null || bgr.Empty())
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(bgr);
        }

        using var hsv = HsvHelper.BgrToHsv(bgr);
        var channels = new Mat[3];
        Cv2.Split(hsv, out channels);

        try
        {
            for (int y = 0; y < hsv.Rows; y++)
            {
                for (int x = 0; x < hsv.Cols; x++)
                {
                    HsvPixelAdjuster.AdjustPixelInMat(hsv, y, x, hueShift, satMult, valMult);
                }
            }

            var result = new Mat();
            Cv2.Merge(channels, hsv);
            Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    public static Mat AdjustHueSatRegion(Mat bgr, Mat mask, double hueShift, double satMult, double valMult)
    {
        var result = bgr.Clone();
        using var hsv = HsvHelper.BgrToHsv(result);
        var channels = new Mat[3];
        Cv2.Split(hsv, out channels);

        try
        {
            for (int y = 0; y < hsv.Rows; y++)
            {
                for (int x = 0; x < hsv.Cols; x++)
                {
                    if (mask.Get<byte>(y, x) == 0) continue;

                    HsvPixelAdjuster.AdjustPixelInMat(hsv, y, x, hueShift, satMult, valMult);
                }
            }

            Cv2.Merge(channels, hsv);
            Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }
}
