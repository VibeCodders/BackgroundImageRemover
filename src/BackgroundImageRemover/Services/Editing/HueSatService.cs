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

        var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var channels = new Mat[3];
        Cv2.Split(hsv, out channels);

        var h = channels[0];
        var s = channels[1];
        var v = channels[2];

        for (int y = 0; y < h.Rows; y++)
        {
            for (int x = 0; x < h.Cols; x++)
            {
                int newH = (int)Math.Round((h.Get<byte>(y, x) + hueShift) % 180);
                if (newH < 0) newH += 180;
                h.Set<byte>(y, x, (byte)newH);

                double newS = s.Get<byte>(y, x) * satMult;
                s.Set<byte>(y, x, (byte)Math.Clamp(newS, 0, 255));

                double newV = v.Get<byte>(y, x) * valMult;
                v.Set<byte>(y, x, (byte)Math.Clamp(newV, 0, 255));
            }
        }

        Cv2.Merge(channels, hsv);
        var result = new Mat();
        Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
        return result;
    }

    public static Mat AdjustHueSatRegion(Mat bgr, Mat mask, double hueShift, double satMult, double valMult)
    {
        var result = bgr.Clone();
        var hsv = new Mat();
        Cv2.CvtColor(result, hsv, ColorConversionCodes.BGR2HSV);

        var channels = new Mat[3];
        Cv2.Split(hsv, out channels);

        var h = channels[0];
        var s = channels[1];
        var v = channels[2];

        for (int y = 0; y < h.Rows; y++)
        {
            for (int x = 0; x < h.Cols; x++)
            {
                if (mask.Get<byte>(y, x) == 0) continue;

                int newH = (int)Math.Round((h.Get<byte>(y, x) + hueShift) % 180);
                if (newH < 0) newH += 180;
                h.Set<byte>(y, x, (byte)newH);

                double newS = s.Get<byte>(y, x) * satMult;
                s.Set<byte>(y, x, (byte)Math.Clamp(newS, 0, 255));

                double newV = v.Get<byte>(y, x) * valMult;
                v.Set<byte>(y, x, (byte)Math.Clamp(newV, 0, 255));
            }
        }

        Cv2.Merge(channels, hsv);
        Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
        return result;
    }
}
