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
        PixelLoop.ForEach(hsv, (y, x) =>
            HsvPixelAdjuster.AdjustPixelInMat(hsv, y, x, hueShift, satMult, valMult));

        return HsvHelper.HsvToBgr(hsv);
    }

    public static Mat AdjustHueSatRegion(Mat bgr, Mat mask, double hueShift, double satMult, double valMult)
    {
        using var hsv = HsvHelper.BgrToHsv(bgr);
        PixelLoop.ForEach(hsv, (y, x) =>
        {
            if (mask.Get<byte>(y, x) == 0)
            {
                return;
            }

            HsvPixelAdjuster.AdjustPixelInMat(hsv, y, x, hueShift, satMult, valMult);
        });

        return HsvHelper.HsvToBgr(hsv);
    }
}
