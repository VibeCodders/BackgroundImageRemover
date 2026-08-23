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
        var hsvSpan = hsv.AsSpan2D<Vec3b>();
        var maskSpan = mask.AsSpan2D<byte>();
        for (int y = 0; y < hsvSpan.Height; y++)
        {
            for (int x = 0; x < hsvSpan.Width; x++)
            {
                if (maskSpan[y, x] == 0)
                {
                    continue;
                }

                HsvPixelAdjuster.AdjustPixel(ref hsvSpan[y, x], hueShift, satMult, valMult);
            }
        }

        return HsvHelper.HsvToBgr(hsv);
    }
}
