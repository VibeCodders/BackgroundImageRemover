using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class NoiseService
{
    public static Mat AddGaussianNoise(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return input.Clone();
        }

        using var noise = new Mat(input.Size(), input.Type());
        Cv2.Randn(noise, 0, strength);

        using var scaled = new Mat();
        noise.ConvertTo(scaled, input.Type(), strength);

        var result = new Mat();
        Cv2.Add(input, scaled, result);
        Cv2.Threshold(result, result, 255, 255, ThresholdTypes.Tozero);
        Cv2.Threshold(result, result, 0, 0, ThresholdTypes.TozeroInv);
        return result;
    }

    public static Mat AddSaltPepperNoise(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return input.Clone();
        }

        var result = input.Clone();
        var rng = new Random();
        int rows = input.Rows;
        int cols = input.Cols;
        int channels = input.Channels();

        double saltProb = strength / 2.0;
        double pepperProb = strength / 2.0;

        if (channels == 1)
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    double r = rng.NextDouble();
                    if (r < saltProb)
                    {
                        result.Set<byte>(y, x, 255);
                    }
                    else if (r < saltProb + pepperProb)
                    {
                        result.Set<byte>(y, x, 0);
                    }
                }
            }
        }
        else
        {
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    double r = rng.NextDouble();
                    if (r < saltProb)
                    {
                        result.Set<Vec3b>(y, x, new Vec3b(255, 255, 255));
                    }
                    else if (r < saltProb + pepperProb)
                    {
                        result.Set<Vec3b>(y, x, new Vec3b(0, 0, 0));
                    }
                }
            }
        }

        return result;
    }
}
