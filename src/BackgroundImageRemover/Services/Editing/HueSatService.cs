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
        HsvPixelAdjuster.AdjustPixelArray(hsv, hueShift, satMult, valMult);

        return HsvHelper.HsvToBgr(hsv);
    }

    public static Mat AdjustHueSatRegion(Mat bgr, Mat mask, double hueShift, double satMult, double valMult)
    {
        using var hsv = HsvHelper.BgrToHsv(bgr);
        Vec3b[] pixels = PixelLoop.GetData<Vec3b>(hsv);
        byte[] maskData = PixelLoop.GetData<byte>(mask);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (maskData[i] == 0)
            {
                continue;
            }

            HsvPixelAdjuster.AdjustPixel(ref pixels[i], hueShift, satMult, valMult);
        }
        PixelLoop.SetData(hsv, pixels);

        return HsvHelper.HsvToBgr(hsv);
    }
}
