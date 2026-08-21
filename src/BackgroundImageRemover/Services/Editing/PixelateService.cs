using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class PixelateService
{
    public static Mat Pixelate(Mat input, int blockSize)
    {
        if (input is null || input.Empty() || blockSize <= 1)
        {
            return input!.Clone();
        }

        int width = input.Cols;
        int height = input.Rows;
        int smallWidth = Math.Max(1, width / blockSize);
        int smallHeight = Math.Max(1, height / blockSize);

        var small = new Mat();
        Cv2.Resize(input, small, new Size(smallWidth, smallHeight), 0, 0, InterpolationFlags.Nearest);

        var result = new Mat();
        Cv2.Resize(small, result, new Size(width, height), 0, 0, InterpolationFlags.Nearest);
        return result;
    }
}
