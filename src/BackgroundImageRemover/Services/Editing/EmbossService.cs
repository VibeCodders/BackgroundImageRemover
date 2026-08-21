using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class EmbossService
{
    public static Mat Emboss(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return input!.Clone();
        }

        using var kernel = new Mat(3, 3, MatType.CV_64F);
        kernel.Set<double>(0, 0, -2);
        kernel.Set<double>(0, 1, -1);
        kernel.Set<double>(0, 2, 0);
        kernel.Set<double>(1, 0, -1);
        kernel.Set<double>(1, 1, 1);
        kernel.Set<double>(1, 2, 1);
        kernel.Set<double>(2, 0, 0);
        kernel.Set<double>(2, 1, 1);
        kernel.Set<double>(2, 2, 2);

        var result = new Mat();
        Cv2.Filter2D(input, result, MatType.CV_16S, kernel);

        if (result.Type() != input.Type())
        {
            result.ConvertTo(result, input.Type());
        }

        var blended = new Mat();
        Cv2.AddWeighted(result, strength, input, 1 - strength, 0, blended);
        return blended;
    }
}
