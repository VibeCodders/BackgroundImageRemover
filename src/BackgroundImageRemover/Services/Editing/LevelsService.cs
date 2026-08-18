using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Levels adjustment (black point, white point, gamma) applied per channel.</summary>
public static class LevelsService
{
    public static Mat Apply(Mat bgr, double blackPoint, double whitePoint, double gamma, LevelsChannel channel = LevelsChannel.Rgb)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        blackPoint = Math.Clamp(blackPoint, 0, 254);
        whitePoint = Math.Clamp(whitePoint, blackPoint + 1, 255);
        gamma = Math.Clamp(gamma, 0.1, 10.0);

        using var lut = BuildLut(blackPoint, whitePoint, gamma);

        if (channel == LevelsChannel.Rgb)
        {
            var result = new Mat();
            Cv2.LUT(bgr, lut, result);
            return result;
        }

        var channels = Cv2.Split(bgr);
        try
        {
            int index = channel switch
            {
                LevelsChannel.Blue => 0,
                LevelsChannel.Green => 1,
                _ => 2 // Red
            };
            Cv2.LUT(channels[index], lut, channels[index]);
            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    private static Mat BuildLut(double black, double white, double gamma)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            if (i <= black)
            {
                lut[i] = 0;
            }
            else if (i >= white)
            {
                lut[i] = 255;
            }
            else
            {
                double t = (i - black) / (white - black);
                lut[i] = (byte)Math.Round(255.0 * Math.Pow(t, 1.0 / gamma));
            }
        }

        var lutMat = new Mat(1, 256, MatType.CV_8UC1);
        lutMat.SetArray(lut);
        return lutMat;
    }
}
