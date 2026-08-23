using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Compositing;
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

        using var split = ChannelSplit.Of(bgr);
        int index = channel switch
        {
            LevelsChannel.Blue => 0,
            LevelsChannel.Green => 1,
            _ => 2 // Red
        };
        using var adjusted = new Mat();
        Cv2.LUT(split[index], lut, adjusted);
        adjusted.CopyTo(split[index]);
        var merged = new Mat();
        Cv2.Merge(split.Channels, merged);
        return merged;
    }

    /// <summary>Stretches each channel's min/max to the full 0–255 range (auto-contrast).</summary>
    public static Mat AutoLevels(Mat bgr)
    {
        using var split = ChannelSplit.Of(bgr);
        for (int i = 0; i < split.Channels.Length; i++)
        {
            Cv2.MinMaxLoc(split[i], out double min, out double max);
            if (max - min < 1.0)
            {
                continue;
            }

            using var lut = BuildLut(min, max, 1.0, 0.0, 255.0);
            using var adjusted = new Mat();
            Cv2.LUT(split[i], lut, adjusted);
            adjusted.CopyTo(split[i]);
        }

        var result = new Mat();
        Cv2.Merge(split.Channels, result);
        return result;
    }

    /// <summary>Applies contrast-limited adaptive histogram equalization in the L channel of LAB space.</summary>
    public static Mat Equalize(Mat bgr, double clipLimit = 2.0, int tileSize = 8)
        => ImageProcessingUtility.ApplyClahe(bgr, clipLimit, tileSize);

    /// <summary>Inverts the image (negative).</summary>
    public static Mat Invert(Mat bgr)
    {
        var result = new Mat();
        Cv2.BitwiseNot(bgr, result);
        return result;
    }

    /// <summary>Neutralizes color casts using a gray-world assumption (per-channel gain).</summary>
    public static Mat AutoWhiteBalance(Mat bgr) => ImageProcessingUtility.AutoWhiteBalance(bgr);

    private static Mat BuildLut(double black, double white, double gamma, double outputBlack, double outputWhite)
    {
        return ImageProcessingUtility.BuildLut(i =>
        {
            if (i <= black) return outputBlack;
            if (i >= white) return outputWhite;
            double t = (i - black) / (white - black);
            return outputBlack + (outputWhite - outputBlack) * Math.Pow(t, 1.0 / gamma);
        });
    }
}
