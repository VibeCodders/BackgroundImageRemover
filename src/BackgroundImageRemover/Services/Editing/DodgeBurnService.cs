using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class DodgeBurnService
{
    public static Mat DodgeBurnRegion(Mat bgr, Mat mask, bool dodge, double strength)
    {
        strength = Math.Clamp(strength, 0, 1);
        var result = bgr.Clone();
        var channels = new Mat[3];
        Cv2.Split(result, out channels);

        for (int i = 0; i < 3; i++)
        {
            var adjusted = new Mat();
            if (dodge)
            {
                Cv2.AddWeighted(channels[i], 1.0 + strength, channels[i], strength, 0, adjusted);
            }
            else
            {
                Cv2.AddWeighted(channels[i], 1.0 - strength, channels[i], -strength, 0, adjusted);
            }
            Cv2.Min(adjusted, new Scalar(255), adjusted);
            Cv2.Max(adjusted, new Scalar(0), adjusted);
            channels[i].CopyTo(result);
        }

        Cv2.Merge(channels, result);
        return result.BlendByMask(result, mask);
    }

    public static Mat DodgeBurnAll(Mat bgr, bool dodge, double strength)
    {
        strength = Math.Clamp(strength, 0, 1);
        var result = bgr.Clone();
        var channels = new Mat[3];
        Cv2.Split(result, out channels);

        for (int i = 0; i < 3; i++)
        {
            var adjusted = new Mat();
            if (dodge)
            {
                Cv2.AddWeighted(channels[i], 1.0 + strength, channels[i], strength, 0, adjusted);
            }
            else
            {
                Cv2.AddWeighted(channels[i], 1.0 - strength, channels[i], -strength, 0, adjusted);
            }
            Cv2.Min(adjusted, new Scalar(255), adjusted);
            Cv2.Max(adjusted, new Scalar(0), adjusted);
            adjusted.CopyTo(channels[i]);
        }

        Cv2.Merge(channels, result);
        return result;
    }
}
