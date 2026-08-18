using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Levels adjustment (black point, white point, gamma) applied per channel.</summary>
public static class LevelsService
{
    public static Mat Apply(Mat bgr, double blackPoint, double whitePoint, double gamma, LevelsChannel channel = LevelsChannel.Rgb)
        => Apply(bgr, blackPoint, whitePoint, gamma, channel, outputBlack: 0.0, outputWhite: 255.0);

    public static Mat Apply(
        Mat bgr,
        double blackPoint,
        double whitePoint,
        double gamma,
        LevelsChannel channel,
        double outputBlack,
        double outputWhite)
    {
        ArgumentNullException.ThrowIfNull(bgr);

        blackPoint = Math.Clamp(blackPoint, 0, 254);
        whitePoint = Math.Clamp(whitePoint, blackPoint + 1, 255);
        gamma = Math.Clamp(gamma, 0.1, 10.0);
        outputBlack = Math.Clamp(outputBlack, 0, 254);
        outputWhite = Math.Clamp(outputWhite, outputBlack + 1, 255);

        using var lut = BuildLut(blackPoint, whitePoint, gamma, outputBlack, outputWhite);

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
            using var adjusted = new Mat();
            Cv2.LUT(channels[index], lut, adjusted);
            adjusted.CopyTo(channels[index]);
            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>Stretches each channel's min/max to the full 0–255 range (auto-contrast).</summary>
    public static Mat AutoLevels(Mat bgr)
    {
        var channels = Cv2.Split(bgr);
        try
        {
            for (int i = 0; i < channels.Length; i++)
            {
                Cv2.MinMaxLoc(channels[i], out double min, out double max);
                if (max - min < 1.0)
                {
                    continue;
                }

                using var lut = BuildLut(min, max, 1.0, 0.0, 255.0);
                using var adjusted = new Mat();
                Cv2.LUT(channels[i], lut, adjusted);
                adjusted.CopyTo(channels[i]);
            }

            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>Applies contrast-limited adaptive histogram equalization in the L channel of LAB space.</summary>
    public static Mat Equalize(Mat bgr, double clipLimit = 2.0, int tileSize = 8)
    {
        using var lab = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);
        var channels = Cv2.Split(lab);
        try
        {
            using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileSize, tileSize));
            using var equalized = new Mat();
            clahe.Apply(channels[0], equalized);
            equalized.CopyTo(channels[0]);

            using var merged = new Mat();
            Cv2.Merge(channels, merged);
            var result = new Mat();
            Cv2.CvtColor(merged, result, ColorConversionCodes.Lab2BGR);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    /// <summary>Inverts the image (negative).</summary>
    public static Mat Invert(Mat bgr)
    {
        var result = new Mat();
        Cv2.BitwiseNot(bgr, result);
        return result;
    }

    /// <summary>Neutralizes color casts using a gray-world assumption (per-channel gain).</summary>
    public static Mat AutoWhiteBalance(Mat bgr)
    {
        var channels = Cv2.Split(bgr);
        try
        {
            double[] means = new double[3];
            double avg = 0.0;
            for (int i = 0; i < 3; i++)
            {
                means[i] = Cv2.Mean(channels[i]).Val0;
                avg += means[i];
            }
            avg /= 3.0;

            for (int i = 0; i < 3; i++)
            {
                double gain = means[i] < 1e-3 ? 1.0 : avg / means[i];
                gain = Math.Clamp(gain, 0.5, 2.0);
                using var adjusted = new Mat();
                channels[i].ConvertTo(adjusted, MatType.CV_8UC1, gain, 0.0);
                adjusted.CopyTo(channels[i]);
            }

            var result = new Mat();
            Cv2.Merge(channels, result);
            return result;
        }
        finally
        {
            foreach (var ch in channels) ch.Dispose();
        }
    }

    private static Mat BuildLut(double black, double white, double gamma, double outputBlack, double outputWhite)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            if (i <= black)
            {
                lut[i] = (byte)outputBlack;
            }
            else if (i >= white)
            {
                lut[i] = (byte)outputWhite;
            }
            else
            {
                double t = (i - black) / (white - black);
                lut[i] = (byte)Math.Round(outputBlack + (outputWhite - outputBlack) * Math.Pow(t, 1.0 / gamma));
            }
        }

        var lutMat = new Mat(1, 256, MatType.CV_8UC1);
        lutMat.SetArray(lut);
        return lutMat;
    }
}
