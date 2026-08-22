using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class RedEyeService
{
    public static Mat RemoveRedEyes(Mat bgr, Point center, double radius)
    {
        if (bgr is null || bgr.Empty())
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(bgr);
        }

        var result = bgr.Clone();
        int r = (int)Math.Round(radius);
        int x1 = Math.Max(0, (int)center.X - r);
        int y1 = Math.Max(0, (int)center.Y - r);
        int x2 = Math.Min(result.Cols - 1, (int)center.X + r);
        int y2 = Math.Min(result.Rows - 1, (int)center.Y + r);

        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                if (dx * dx + dy * dy > r * r) continue;

                Vec3b pixel = result.Get<Vec3b>(y, x);
                byte b = pixel[0];
                byte g = pixel[1];
                byte rv = pixel[2];

                if (rv > g && rv > b && rv > 80)
                {
                    double avg = (g + b) / 2.0;
                    byte newVal = (byte)Math.Clamp(avg, 0, 255);
                    result.Set<Vec3b>(y, x, new Vec3b(newVal, newVal, newVal));
                }
            }
        }

        return result;
    }
}
