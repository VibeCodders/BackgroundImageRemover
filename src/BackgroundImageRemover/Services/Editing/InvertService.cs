using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class InvertService
{
    public static Mat Invert(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return input!.Clone();
        }

        if (strength >= 1)
        {
            using var invertedFull = new Mat();
            Cv2.BitwiseNot(input, invertedFull);
            return invertedFull;
        }

        using var inverted = new Mat();
        Cv2.BitwiseNot(input, inverted);

        var result = new Mat();
        Cv2.AddWeighted(input, 1 - strength, inverted, strength, 0, result);
        return result;
    }
}
