using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class InvertService
{
    public static Mat Invert(Mat input, double strength)
    {
        if (input is null || input.Empty())
        {
            return input.CloneOrEmpty();
        }

        using var inverted = new Mat();
        Cv2.BitwiseNot(input, inverted);

        return ImageProcessingUtility.BlendLinear(input, inverted, strength);
    }
}
